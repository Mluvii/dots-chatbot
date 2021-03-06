﻿using System.Threading;
using System.Threading.Tasks;
using FinanceBot.BotAssets;
using FinanceBot.BotAssets.Dialogs;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Internals.Fibers;
using Microsoft.Bot.Builder.Scorables;
using Microsoft.Bot.Connector;

#pragma warning disable 1998
namespace FinanceBot.Dialogs
{
    public class HelpScorable : IScorable<IActivity, double>
    {
        private readonly IDialogFactory dialogFactory;
        private readonly IDialogTask task;

        public HelpScorable(IDialogTask task, IDialogFactory dialogFactory)
        {
            SetField.NotNull(out this.task, nameof(task), task);
            SetField.NotNull(out this.dialogFactory, nameof(dialogFactory), dialogFactory);
        }

        public async Task<object> PrepareAsync(IActivity item, CancellationToken token)
        {
            var message = item as IMessageActivity;

            if (!string.IsNullOrWhiteSpace(message?.Text))
                if (RegexConstants.CONFUSED.IsMatch(StringUtils.RemoveDiacritics(message.Text)))
                    return message.Text;

            return null;
        }

        public bool HasScore(IActivity item, object state)
        {
            return state != null;
        }

        public double GetScore(IActivity item, object state)
        {
            var matched = state != null;
            var score = matched ? 1.0 : double.NaN;
            return score;
        }

        public async Task PostAsync(IActivity item, object state, CancellationToken token)
        {
            var message = item as IMessageActivity;

            if (message != null)
            {
                var helpDialog = dialogFactory.Create<HelpDialog>();

                // wrap it with an additional dialog that will restart the wait for
                // messages from the user once the child dialog has finished
                var interruption = helpDialog.Void<object, IMessageActivity>();

                // put the interrupting dialog on the stack
                task.Call(interruption, null);

                // start running the interrupting dialog
                await task.PollAsync(token);
            }
        }

        public Task DoneAsync(IActivity item, object state, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}