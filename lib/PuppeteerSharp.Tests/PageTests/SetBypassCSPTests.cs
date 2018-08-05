﻿using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.PageTests
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class SetBypassCSPTests : PuppeteerPageBaseTest
    {
        public SetBypassCSPTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ShouldBypassCSPMetaTag()
        {
            // Make sure CSP prohibits addScriptTag.
            await Page.GoToAsync(TestConstants.ServerUrl + "/csp.html");
            await Page.AddScriptTagAsync(new AddTagOptions
            {
                Content = "window.__injected = 42;"
            });
            Assert.Null(await Page.EvaluateExpressionAsync("window.__injected"));

            // By-pass CSP and try one more time.
            await Page.SetBypassCSPAsync(true);
            await Page.ReloadAsync();
            await Page.AddScriptTagAsync(new AddTagOptions
            {
                Content = "window.__injected = 42;"
            });
            Assert.Equal(42, await Page.EvaluateExpressionAsync<int>("window.__injected"));
        }

        [Fact]
        public async Task ShouldBypassCSPHeader()
        {
            // Make sure CSP prohibits addScriptTag.
            Server.SetCSP("/empty.html", "default-src 'self'");
            await Page.GoToAsync(TestConstants.EmptyPage);
            await Page.AddScriptTagAsync(new AddTagOptions
            {
                Content = "window.__injected = 42;"
            });
            Assert.Null(await Page.EvaluateExpressionAsync("window.__injected"));

            // By-pass CSP and try one more time.
            await Page.SetBypassCSPAsync(true);
            await Page.ReloadAsync();
            await Page.AddScriptTagAsync(new AddTagOptions
            {
                Content = "window.__injected = 42;"
            });
            Assert.Equal(42, await Page.EvaluateExpressionAsync<int>("window.__injected"));
        }

        [Fact]
        public async Task ShouldBypassAfterCrossProcessNavigation()
        {
            await Page.SetBypassCSPAsync(true);
            await Page.GoToAsync(TestConstants.ServerUrl + "/csp.html");
            await Page.AddScriptTagAsync(new AddTagOptions
            {
                Content = "window.__injected = 42;"
            });
            Assert.Equal(42, await Page.EvaluateExpressionAsync<int>("window.__injected"));

            await Page.GoToAsync(TestConstants.CrossProcessUrl + "/csp.html");
            await Page.AddScriptTagAsync(new AddTagOptions
            {
                Content = "window.__injected = 42;"
            });
            Assert.Equal(42, await Page.EvaluateExpressionAsync<int>("window.__injected"));
        }
    }
}