using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PuppeteerSharp.Threading;
using Xunit;

namespace PuppeteerSharp.Tests.Threading
{
    public class EventInvocationListTests
    {
        [Fact]
        public async Task ShouldInvokeHandlersInOrder()
        {
            var target = new EventInvocationList<EventArgs>();

            Assert.True(target.IsEmpty);

            List<int> results = new List<int>();

            target.Add(async (s, e) =>
            {
                await Task.Delay(100);
                results.Add(1);
            });
            target.Add((s, e) =>
            {
                results.Add(2);
                return Task.Delay(50);
            });
            target.Add((s, e) => results.Add(3));

            await target.InvokeAsync(this, EventArgs.Empty);

            Assert.Collection(results,
                x => Assert.Equal(1, x),
                x => Assert.Equal(2, x),
                x => Assert.Equal(3, x));

            results.Clear();

            target.Invoke(this, EventArgs.Empty);

            Assert.Collection(results,
                x => Assert.Equal(1, x),
                x => Assert.Equal(2, x),
                x => Assert.Equal(3, x));
        }

        [Fact]
        public async Task ShouldAllowSelfUnregistering()
        {
            var target = new EventInvocationList<EventArgs>();

            Task Handler(object sender, EventArgs e)
            {
                target.Remove(Handler);
                return Task.CompletedTask;
            }

            target.Add(Handler);
            target.Add(Handler);

            Assert.True(!target.IsEmpty);

            await target.InvokeAsync(this, EventArgs.Empty);

            Assert.True(target.IsEmpty);
        }
    }
}
