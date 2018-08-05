﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.PageTests.Events
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class DialogTests : PuppeteerPageBaseTest
    {
        public DialogTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ShouldFire()
        {
            Page.DialogAsync += async (sender, e) =>
            {
                Assert.Equal(DialogType.Alert, e.Dialog.DialogType);
                Assert.Equal(string.Empty, e.Dialog.DefaultValue);
                Assert.Equal("yo", e.Dialog.Message);

                await e.Dialog.Accept();
            };

            await Page.EvaluateExpressionAsync("alert('yo');");
        }

        [Fact]
        public async Task ShouldAllowAcceptingPrompts()
        {
            Page.DialogAsync += async (sender, e) =>
            {
                Assert.Equal(DialogType.Prompt, e.Dialog.DialogType);
                Assert.Equal("yes.", e.Dialog.DefaultValue);
                Assert.Equal("question?", e.Dialog.Message);

                await e.Dialog.Accept("answer!");
            };

            var result = await Page.EvaluateExpressionAsync<string>("prompt('question?', 'yes.')");
            Assert.Equal("answer!", result);
        }

        [Fact]
        public async Task ShouldDismissThePrompt()
        {
            Page.DialogAsync += async (sender, e) =>
            {
                await e.Dialog.Dismiss();
            };

            var result = await Page.EvaluateExpressionAsync<string>("prompt('question?')");
            Assert.Null(result);
        }
    }
}
