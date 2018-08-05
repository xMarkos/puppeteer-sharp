using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuppeteerSharp.Threading
{
    /// <summary>
    /// A data structure that handles both synchronous and asynchronous event handlers.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
    internal class EventInvocationList<TEventArgs>
    {
        private List<Delegate> _delegates = new List<Delegate>();

        /// <summary>
        /// Gets value indicating if this invocation list is empty.
        /// </summary>
        public bool IsEmpty => _delegates.Count == 0;

        /// <summary>
        /// Adds a synchronous event handler.
        /// </summary>
        /// <param name="handler">The handler to be added.</param>
        public void Add(EventHandler<TEventArgs> handler)
            => Add((Delegate)handler);

        /// <summary>
        /// Adds an asynchronous event handler.
        /// </summary>
        /// <param name="handler">The handler to be added.</param>
        public void Add(AsyncEventHandler<TEventArgs> handler)
            => Add((Delegate)handler);

        /// <summary>
        /// Removes a synchronous event handler.
        /// </summary>
        /// <param name="handler">The handler to be removed.</param>
        public void Remove(EventHandler<TEventArgs> handler)
            => Remove((Delegate)handler);

        /// <summary>
        /// Removes an synchronous event handler.
        /// </summary>
        /// <param name="handler">The handler to be removed.</param>
        public void Remove(AsyncEventHandler<TEventArgs> handler)
            => Remove((Delegate)handler);

        private void Add(Delegate handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            foreach (Delegate d in handler.GetInvocationList())
            {
                _delegates.Add(d);
            }
        }

        private void Remove(Delegate handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            foreach (Delegate d in handler.GetInvocationList())
            {
                _delegates.Remove(d);
            }
        }

        /// <summary>
        /// Asynchronously invokes te handlers in this instance.
        /// </summary>
        /// <param name="sender">The sender of this event.</param>
        /// <param name="args">The event arguments.</param>
        /// <returns>
        /// The handlers are invoked one-by-one in the order of registration. Each asynchronous
        /// handler is awaited before the next one is invoked.
        /// </returns>
        public async Task InvokeAsync(object sender, TEventArgs args)
        {
            // The copy of the list is needed to be able to add/remove handlers during invoking (enumerator would throw on modification)
            foreach (Delegate d in new List<Delegate>(_delegates))
            {
                if (d is AsyncEventHandler<TEventArgs> a)
                {
                    await a(sender, args).ConfigureAwait(false);
                }
                else if (d is EventHandler<TEventArgs> s)
                {
                    s(sender, args);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// Synchronously invokes the handlers in this instance.
        /// </summary>
        /// <param name="sender">The sender of this event.</param>
        /// <param name="args">The event arguments.</param>
        /// <remarks>
        /// Because the event handlers (including the async ones) are invoked synchronously,
        /// there is a risk that the call to this method deadlocks. Use with caution!
        /// </remarks>
        public void Invoke(object sender, TEventArgs args)
        {
            foreach (Delegate d in new List<Delegate>(_delegates))
            {
                if (d is AsyncEventHandler<TEventArgs> a)
                {
                    a(sender, args).GetAwaiter().GetResult();
                }
                else if (d is EventHandler<TEventArgs> s)
                {
                    s(sender, args);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// Returns the invocation list of this instance.
        /// </summary>
        /// <returns>An array of delegates representing the invocation list of this instance.</returns>
        public Delegate[] GetInvocationList()
            => _delegates.ToArray();
    }
}
