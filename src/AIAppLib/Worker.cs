// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AIAppLib;

public sealed class Worker([FromKeyedServices("MyKernel")] Kernel kernel) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get chat completion service
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        // Enable auto function calling
        PromptExecutionSettings promptExecutionSettings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        Console.Write("> ");

        var history = new ChatHistory();

        string? input = null;
        while ((input = Console.ReadLine()) is not null)
        {
            try
            {
                Console.WriteLine();

                history.AddMessage(AuthorRole.User, input);

                ChatMessageContent chatResult = await chatCompletionService.GetChatMessageContentAsync(history, promptExecutionSettings, kernel, stoppingToken);

                history.Add(chatResult);

                Console.Write($"\n>>> Result2: {chatResult}\n\n> ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n>>> Exception: {ex.Message}\n\n> ");
            }
        }
    }
}
