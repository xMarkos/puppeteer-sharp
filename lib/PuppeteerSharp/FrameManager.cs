using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PuppeteerSharp.Messaging;
using PuppeteerSharp.Threading;

namespace PuppeteerSharp
{
    internal class FrameManager
    {
        private readonly CDPSession _client;
        private readonly Page _page;
        private Dictionary<int, ExecutionContext> _contextIdToContext;
        private readonly ILogger _logger;

        internal FrameManager(CDPSession client, FrameTree frameTree, Page page)
        {
            _client = client;
            _page = page;
            Frames = new Dictionary<string, Frame>();
            _contextIdToContext = new Dictionary<int, ExecutionContext>();
            _logger = _client.Connection.LoggerFactory.CreateLogger<FrameManager>();

            _client.MessageReceived += OnMessageReceived;
            HandleFrameTree(frameTree).GetAwaiter().GetResult();
        }

        #region Properties
        internal event AsyncEventHandler<FrameEventArgs> FrameAttached;
        internal event AsyncEventHandler<FrameEventArgs> FrameDetached;
        internal event AsyncEventHandler<FrameEventArgs> FrameNavigated;
        internal event AsyncEventHandler<FrameEventArgs> FrameNavigatedWithinDocument;
        internal event AsyncEventHandler<FrameEventArgs> LifecycleEvent;

        internal Dictionary<string, Frame> Frames { get; set; }
        internal Frame MainFrame { get; set; }

        #endregion

        #region Public Methods

        internal JSHandle CreateJSHandle(int contextId, dynamic remoteObject)
        {
            _contextIdToContext.TryGetValue(contextId, out var storedContext);

            if (storedContext == null)
            {
                _logger.LogError("INTERNAL ERROR: missing context with id = {ContextId}", contextId);
            }

            if (remoteObject.subtype == "node")
            {
                return new ElementHandle(storedContext, _client, remoteObject, _page, this);
            }

            return new JSHandle(storedContext, _client, remoteObject);
        }

        #endregion

        #region Private Methods

        private Task OnMessageReceived(object sender, MessageEventArgs e)
        {
            switch (e.MessageID)
            {
                case "Page.frameAttached":
                    return OnFrameAttached(
                        e.MessageData.SelectToken("frameId").ToObject<string>(),
                        e.MessageData.SelectToken("parentFrameId").ToObject<string>());

                case "Page.frameNavigated":
                    return OnFrameNavigated(e.MessageData.SelectToken("frame").ToObject<FramePayload>());

                case "Page.navigatedWithinDocument":
                    return OnFrameNavigatedWithinDocument(e.MessageData.ToObject<NavigatedWithinDocumentResponse>());

                case "Page.frameDetached":
                    return OnFrameDetached(e.MessageData.ToObject<BasicFrameResponse>());

                case "Page.frameStoppedLoading":
                    return OnFrameStoppedLoading(e.MessageData.ToObject<BasicFrameResponse>());

                case "Runtime.executionContextCreated":
                    return OnExecutionContextCreated(e.MessageData.SelectToken("context").ToObject<ContextPayload>());

                case "Runtime.executionContextDestroyed":
                    return OnExecutionContextDestroyed(e.MessageData.SelectToken("executionContextId").ToObject<int>());

                case "Runtime.executionContextsCleared":
                    return OnExecutionContextsCleared();

                case "Page.lifecycleEvent":
                    return OnLifeCycleEvent(e.MessageData.ToObject<LifecycleEventResponse>());

                default:
                    return Task.CompletedTask;
            }
        }

        private async Task OnFrameStoppedLoading(BasicFrameResponse e)
        {
            if (Frames.TryGetValue(e.FrameId, out var frame))
            {
                frame.OnLoadingStopped();
                await LifecycleEvent.SafeInvoke(this, new FrameEventArgs(frame)).ConfigureAwait(false);
            }
        }

        private async Task OnLifeCycleEvent(LifecycleEventResponse e)
        {
            if (Frames.TryGetValue(e.FrameId, out var frame))
            {
                frame.OnLifecycleEvent(e.LoaderId, e.Name);
                await LifecycleEvent.SafeInvoke(this, new FrameEventArgs(frame)).ConfigureAwait(false);
            }
        }

        private Task OnExecutionContextsCleared()
        {
            foreach (var context in _contextIdToContext.Values)
            {
                RemoveContext(context);
            }

            _contextIdToContext.Clear();

            return Task.CompletedTask;
        }

        private Task OnExecutionContextDestroyed(int executionContextId)
        {
            _contextIdToContext.TryGetValue(executionContextId, out var context);

            if (context != null)
            {
                _contextIdToContext.Remove(executionContextId);
                RemoveContext(context);
            }

            return Task.CompletedTask;
        }

        private Task OnExecutionContextCreated(ContextPayload contextPayload)
        {
            var frameId = contextPayload.AuxData.IsDefault ? contextPayload.AuxData.FrameId : null;
            var frame = !string.IsNullOrEmpty(frameId) ? Frames[frameId] : null;

            var context = new ExecutionContext(
                _client,
                contextPayload,
                remoteObject => CreateJSHandle(contextPayload.Id, remoteObject),
                frame);

            _contextIdToContext[contextPayload.Id] = context;

            if (frame != null)
            {
                frame.SetDefaultContext(context);
            }

            return Task.CompletedTask;
        }

        private async Task OnFrameDetached(BasicFrameResponse e)
        {
            if (Frames.TryGetValue(e.FrameId, out var frame))
            {
                await RemoveFramesRecursively(frame).ConfigureAwait(false);
            }
        }

        private async Task OnFrameNavigated(FramePayload framePayload)
        {
            var isMainFrame = string.IsNullOrEmpty(framePayload.ParentId);
            var frame = isMainFrame ? MainFrame : Frames[framePayload.Id];

            Contract.Assert(isMainFrame || frame != null, "We either navigate top level or have old version of the navigated frame");

            // Detach all child frames first.
            if (frame != null)
            {
                while (frame.ChildFrames.Count > 0)
                {
                    await RemoveFramesRecursively(frame.ChildFrames[0]).ConfigureAwait(false);
                }
            }

            // Update or create main frame.
            if (isMainFrame)
            {
                if (frame != null)
                {
                    // Update frame id to retain frame identity on cross-process navigation.
                    if (frame.Id != null)
                    {
                        Frames.Remove(frame.Id);
                    }
                    frame.Id = framePayload.Id;
                }
                else
                {
                    // Initial main frame navigation.
                    frame = new Frame(_client, _page, null, framePayload.Id);
                }

                Frames[framePayload.Id] = frame;
                MainFrame = frame;
            }

            // Update frame payload.
            frame.Navigated(framePayload);

            await FrameNavigated.SafeInvoke(this, new FrameEventArgs(frame)).ConfigureAwait(false);
        }

        private async Task OnFrameNavigatedWithinDocument(NavigatedWithinDocumentResponse e)
        {
            if (Frames.TryGetValue(e.FrameId, out var frame))
            {
                frame.NavigatedWithinDocument(e.Url);

                var eventArgs = new FrameEventArgs(frame);
                await FrameNavigatedWithinDocument.SafeInvoke(this, eventArgs).ConfigureAwait(false);
                await FrameNavigated.SafeInvoke(this, eventArgs).ConfigureAwait(false);
            }
        }

        private void RemoveContext(ExecutionContext context)
        {
            if (context.Frame != null)
            {
                context.Frame.SetDefaultContext(null);
            }
        }

        private async Task RemoveFramesRecursively(Frame frame)
        {
            while (frame.ChildFrames.Count > 0)
            {
                await RemoveFramesRecursively(frame.ChildFrames[0]).ConfigureAwait(false);
            }
            frame.Detach();
            Frames.Remove(frame.Id);
            await FrameDetached.SafeInvoke(this, new FrameEventArgs(frame)).ConfigureAwait(false);
        }

        private async Task OnFrameAttached(string frameId, string parentFrameId)
        {
            if (!Frames.ContainsKey(frameId) && Frames.ContainsKey(parentFrameId))
            {
                var parentFrame = Frames[parentFrameId];
                var frame = new Frame(_client, _page, parentFrame, frameId);
                Frames[frame.Id] = frame;
                await FrameAttached.SafeInvoke(this, new FrameEventArgs(frame)).ConfigureAwait(false);
            }
        }

        private async Task HandleFrameTree(FrameTree frameTree)
        {
            if (!string.IsNullOrEmpty(frameTree.Frame.ParentId))
            {
                await OnFrameAttached(frameTree.Frame.Id, frameTree.Frame.ParentId).ConfigureAwait(false);
            }

            await OnFrameNavigated(frameTree.Frame).ConfigureAwait(false);

            if (frameTree.Childs != null)
            {
                foreach (var child in frameTree.Childs)
                {
                    await HandleFrameTree(child).ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}
