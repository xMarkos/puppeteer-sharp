using System;
using System.Threading.Tasks;

namespace PuppeteerSharp.Threading
{
    /// <summary>
    /// Class defining extension methods for event handlers.
    /// </summary>
    internal static class EventExtensions
    {
        /// <summary>
        /// Safely invokes an asynchronous event handler.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="obj">The target instance.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The arguments of the event.</param>
        /// <returns>A task rerpesenting the asynchronous operation.</returns>
        public static Task SafeInvoke<TEventArgs>(this AsyncEventHandler<TEventArgs> obj, object sender, TEventArgs args)
        {
            if (obj == null)
            {
                return Task.CompletedTask;
            }

            return obj(sender, args);
        }

        /// <summary>
        /// Safely invokes an asynchronous event handler.
        /// </summary>
        /// <param name="obj">The target instance.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The arguments of the event.</param>
        /// <returns>A task rerpesenting the asynchronous operation.</returns>
        public static Task SafeInvoke(this AsyncEventHandler obj, object sender, EventArgs args)
        {
            if (obj == null)
            {
                return Task.CompletedTask;
            }

            return obj(sender, args);
        }

        /// <summary>
        /// Safely invokes an event handler.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="obj">The target instance.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The arguments of the event.</param>
        public static void SafeInvoke<TEventArgs>(this EventHandler<TEventArgs> obj, object sender, TEventArgs args)
            => obj?.Invoke(sender, args);

        /// <summary>
        /// Safely invokes an event handler.
        /// </summary>
        /// <param name="obj">The target instance.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The arguments of the event.</param>
        public static void SafeInvoke(this EventHandler obj, object sender, EventArgs args)
            => obj?.Invoke(sender, args);

        /// <summary>
        /// Converts the non-generic delegate to equivalent generic delegate.
        /// </summary>
        /// <param name="handler">The delegate to be converted.</param>
        /// <returns>The converted delegate.</returns>
        /// <remarks>
        /// The conversion is type safe as both delegate types have the same method signature.
        /// Casting the same delegate twice creates two instances that are equal.
        /// </remarks>
        public static EventHandler<EventArgs> ConvertToGenericDelegate(this EventHandler handler)
            => (EventHandler<EventArgs>)ConvertDelegate<EventHandler<EventArgs>>(handler);

        private static Delegate ConvertDelegate<TResult>(Delegate del)
        {
            if (del == null)
            {
                return null;
            }

            Delegate[] delegates = del.GetInvocationList();
            for (int i = 0; i < delegates.Length; i++)
            {
                Delegate d = delegates[i];
                delegates[i] = Delegate.CreateDelegate(typeof(TResult), d.Target, d.Method);
            }

            return Delegate.Combine(delegates);
        }
    }
}
