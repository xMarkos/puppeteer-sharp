using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PuppeteerSharp.Threading;
using Xunit;

namespace PuppeteerSharp.Tests.Threading
{
    public class EventExtensionsTests
    {
        [Fact]
        public void ConvertedDelegatesShouldBeEqual()
        {
            EventHandler handler = (sender, e) => { };

            EventHandler<EventArgs> result1 = handler.ConvertToGenericDelegate();
            EventHandler<EventArgs> result2 = handler.ConvertToGenericDelegate();

            Assert.False(Object.ReferenceEquals(result1, result2));
            Assert.True(result1 == result2);
        }

        [Fact]
        public void ConvertedDelegatesShouldBeInvokable()
        {
            bool invoked = false;
            EventHandler handler = (sender, e) => invoked = true;
            EventHandler<EventArgs> target = handler.ConvertToGenericDelegate();

            Assert.False(invoked);

            target(this, EventArgs.Empty);

            Assert.True(invoked);
        }

        [Fact]
        public async Task SafeInvokeShouldNotThrowForNullDelegate()
        {
            EventHandler h1 = null;
            EventHandler<EventArgs> h2 = null;
            AsyncEventHandler h3 = null;
            AsyncEventHandler<EventArgs> h4 = null;

            h1.SafeInvoke(this, EventArgs.Empty);
            h2.SafeInvoke(this, EventArgs.Empty);
            await h3.SafeInvoke(this, EventArgs.Empty);
            await h4.SafeInvoke(this, EventArgs.Empty);
        }

        [Fact]
        public async Task SafeInvokeShouldInvokeDelegate()
        {
            List<int> results = new List<int>();

            EventHandler h1 = (sender, e) => results.Add(1);
            EventHandler<EventArgs> h2 = (sender, e) => results.Add(2);
            AsyncEventHandler h3 = async (sender, e) =>
            {
                await Task.Yield();
                results.Add(3);
            };
            AsyncEventHandler<EventArgs> h4 = async (sender, e) =>
            {
                await Task.Yield();
                results.Add(4);
            };

            Assert.Empty(results);

            h1.SafeInvoke(this, EventArgs.Empty);
            h2.SafeInvoke(this, EventArgs.Empty);
            await h3.SafeInvoke(this, EventArgs.Empty);
            await h4.SafeInvoke(this, EventArgs.Empty);

            Assert.Equal(new[] { 1, 2, 3, 4 }, results);
        }
    }
}
