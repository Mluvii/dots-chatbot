﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceBot.BotAssets.Dialogs;
using FinanceBot.BotAssets.Models;
using FinanceBot.Properties;
using iCord.OnifWebLib.Linq;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinanceBot.Dialogs
{
    public class AvailibleOperatorsDialog : IDialog<AvailableOperatorInfo>
    {
        private readonly IDialogFactory dialogFactory;
        private GetAvailableOperatorsResponse availibleOperators;

        private int maxAttempts = 3;

        public AvailibleOperatorsDialog(IDialogFactory dialogFactory)
        {
            this.dialogFactory = dialogFactory;
        }

        public async Task StartAsync(IDialogContext context)
        {
            await AskServerForAvailableOperators(context);
            context.Wait(OnMessageRecieved);
        }


        private async Task OnMessageRecieved(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var activity = await result;
            if (activity.AsEventActivity() != null && activity.ChannelData != null)
                try
                {
                    var availibleOperatorsInfo =
                        JsonConvert.DeserializeObject<GetAvailableOperatorsResponse>(activity.ChannelData);
                    await OnAvailibleOperatorsResponse(context, availibleOperatorsInfo);
                    return;
                }
                catch (Exception)
                {
                    context.Done<GetAvailableOperatorsResponse>(null);
                    return;
                }

            if (maxAttempts > 0)
            {
                await context.SayAsync(Resources.OperatorConnect_still_looking);
                maxAttempts--;
                await AskServerForAvailableOperators(context);
                context.Wait(OnMessageRecieved);
                return;
            }

            await context.SayAsync(Resources.OperatorSelection_none_availible);
            context.Done<AvailableOperatorInfo>(null);
        }

        private async Task OnAvailibleOperatorsResponse(IDialogContext context, GetAvailableOperatorsResponse result)
        {
            availibleOperators = result;
            availibleOperators.AvailableOperators =
                availibleOperators.AvailableOperators.DistinctBy(ope => ope.DisplayName).ToList();
            if (availibleOperators.AvailableOperators.Count > 1)
            {
                var operatorNames = availibleOperators.AvailableOperators.Select(ope => ope.DisplayName).ToList();
                operatorNames.Sort();
                await context.SayAsync(string.Format(Resources.OperatorConnect_available_list, $"{string.Join(", ", operatorNames.Take(operatorNames.Count - 1))} {Resources.and} {operatorNames.Last()}"));
                PromptDialog.Choice(context, OnOperatorSelected, operatorNames, Resources.OperatorConnect_select_operator,
                    Resources.RetryText, 5);
                return;
            }

            if (availibleOperators.AvailableOperators.Count == 1)
            {
                var operatorName = availibleOperators.AvailableOperators.Single().DisplayName;
                await OnOperatorSelected(context, new AwaitableFromItem<string>(operatorName));
                return;
            }

            await context.SayAsync(Resources.OperatorSelection_none_availible);
            context.Done<AvailableOperatorInfo>(null);
        }

        private async Task OnOperatorSelected(IDialogContext context, IAwaitable<string> result)
        {
            string selectedOpe = null;
            try
            {
                selectedOpe = await result;
            }
            catch (TooManyAttemptsException)
            {
                context.Call(dialogFactory.Create<HelpDialog, bool>(false), null);
                return;
            }

            var selected = availibleOperators.AvailableOperators.SingleOrDefault(ope => ope.DisplayName.Equals(selectedOpe));

            context.Done(selected);
        }

        private async Task AskServerForAvailableOperators(IDialogContext context)
        {
            var a = context.Activity as Activity;
            var data = JObject.Parse(@"{ ""Activity"": ""GetAvailableOperators"" }");
            var act = context.MakeMessage();
            act.ChannelData = data;
            await context.PostAsync(act);
        }
    }
}
