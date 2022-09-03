using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

class Bot
{
    public static ITelegramBotClient bot = new TelegramBotClient("<Insert the token you've got from BotFather here>");
    public static async Task HandleUpdateAsync
        (ITelegramBotClient bot, Update u, CancellationToken cToken)
    {
        try
        {
            if (u.Type == UpdateType.Message)
            {
                var userId = u.Message.From.Id;
                var message = u.Message;
                var type = message.Type;
                var chatId = message.Chat.Id;
                var userFolder = $"Downloads\\{userId}";

                Directory.CreateDirectory(userFolder);
                var filesNumber = Directory.GetFiles($"Downloads\\{userId}", "*", SearchOption.TopDirectoryOnly).Length;

                Console.WriteLine(message.Type + " from " + message.From.FirstName + (message.Text != null ? ": " + 
                                  message.Text : string.Empty));

                if (message.Text == "/start")
                {
                    await bot.SendTextMessageAsync(
                        chatId,
                        text: "Try sending me a file >>");

                    return;
                }

                if (message.Text == "/files")
                {
                    ShowFilesAsync(bot, userFolder, chatId);
                    return;
                }

                if (message.Text == "/delete")
                {
                    if (filesNumber == 0)
                    {
                        await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                        await bot.SendTextMessageAsync(chatId, "There are no files to delete");

                        return;
                    }

                    var yesButton = InlineKeyboardButton.WithCallbackData("Yes");
                    var noButton = InlineKeyboardButton.WithCallbackData("No");

                    var yesButtonRow = new InlineKeyboardButton[] { yesButton };
                    var noButtonRow = new InlineKeyboardButton[] { noButton };

                    var buttonArray = new InlineKeyboardButton[][] { yesButtonRow, noButtonRow };

                    InlineKeyboardMarkup ikButtons = new InlineKeyboardMarkup(buttonArray);

                    await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                    await bot.SendTextMessageAsync(chatId, "Are you sure you want to remove all the files?", replyMarkup: ikButtons);

                    return;
                }

                if (type == MessageType.Photo)
                {
                    var fileName = $"Photo{filesNumber + 1}.jpeg";

                    DownloadFileAsync(message.Photo.Last().FileId, userFolder, bot, chatId, fileName);
                    return;
                }

                if (type == MessageType.Voice)
                {
                    var fileName = $"Voice{filesNumber + 1}.mp3";

                    DownloadFileAsync(message.Voice.FileId, userFolder, bot, chatId, fileName);
                    return;
                }

                if (type == MessageType.Audio)
                {
                    var audio = message.Audio;
                    DownloadFileAsync(message.Audio.FileId, userFolder, bot, chatId, audio.FileName);
                    return;
                }

                if (type == MessageType.Video)
                {
                    var fileName = $"Video{filesNumber + 1}.mp4";

                    DownloadFileAsync(message.Video.FileId, userFolder, bot, chatId, fileName);
                    return;
                }

                if (type == MessageType.Document)
                {
                    var document = message.Document;
                    if (document.FileName.EndsWith(".gif.mp4"))
                        document.FileName = $"Animation{filesNumber + 1}.mp4";

                    DownloadFileAsync(document.FileId, userFolder, bot, chatId, document.FileName);
                    return;
                }

                if (type == MessageType.Text)
                {
                    await bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    await bot.SendTextMessageAsync(chatId, message.Text);
                    return;
                }

                return;
            }
            if (u.Type == UpdateType.CallbackQuery)
            {
                var cbQuery = u.CallbackQuery;
                var userId = cbQuery.From.Id;
                var userFolder = $"Downloads\\{userId}";
                var chatId = cbQuery.Message.Chat.Id;

                Directory.CreateDirectory(userFolder);
                string[] filesArray = Directory.GetFiles(userFolder, "*", SearchOption.TopDirectoryOnly);

                Console.WriteLine("Callback from " + cbQuery.From.FirstName.ToString() + ": " + cbQuery.Data.ToString());

                if (cbQuery.Data == "Yes")
                {
                    if (filesArray.Length == 0)
                    {
                        await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                        await bot.SendTextMessageAsync(chatId, "The folder is already empty");
                        return;
                    }

                    DirectoryInfo dir = new DirectoryInfo(userFolder);
                    foreach (FileInfo file in dir.EnumerateFiles())
                    {
                        file.Delete();
                    }

                    await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                    await bot.SendTextMessageAsync(chatId, "All the files have been removed");
                    return;
                }

                if (cbQuery.Data == "No")
                {
                    if (filesArray.Length == 0)
                    {
                        await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                        await bot.SendTextMessageAsync(chatId, "The folder is already empty");
                        return;
                    }

                    await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                    await bot.SendTextMessageAsync(chatId, "You have cancelled files deletion");
                    return;
                }

                if (filesArray.Length == 0)
                {
                    await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                    await bot.SendTextMessageAsync(chatId, "The file has not been found");
                    return;
                }

                for (int i = 0; i < filesArray.Length; i++)
                {
                    if (Path.GetFileNameWithoutExtension(filesArray[i]) == cbQuery.Data)
                    {
                        await bot.SendChatActionAsync(chatId, ChatAction.Typing);
                        Message uploadMessage = await bot.SendTextMessageAsync(chatId, "Downloading...");

                        await bot.SendChatActionAsync(chatId, ChatAction.UploadDocument);
                        await using Stream stream = System.IO.File.OpenRead(filesArray[i]);

                        await bot.SendDocumentAsync(
                            chatId: chatId,
                            document: new InputOnlineFile(content: stream, fileName: Path.GetFileName(filesArray[i]))
                            );

                        await bot.DeleteMessageAsync(chatId, uploadMessage.MessageId, cToken);

                        return;
                    }
                }
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public static async Task HandleErrorAsync
        (ITelegramBotClient bot, Exception ex, CancellationToken cToken)
    {
        Console.WriteLine(ex.Message);
    }

    public static async void DownloadFileAsync
        (string fileId, string path, ITelegramBotClient bot, long chatId, string fileName)
    {
        await bot.SendChatActionAsync(chatId, ChatAction.Typing);

        var fileInfo = await bot.GetFileAsync(fileId);
        var filePath = fileInfo.FilePath;

        using FileStream fileStream = System.IO.File.OpenWrite(path + @$"\{fileName}");
        await bot.DownloadFileAsync(
            filePath: filePath,
            destination: fileStream);

        await bot.SendTextMessageAsync(chatId, text: $"The file has been successfully saved as \n{fileName}");
    }

    public static async void ShowFilesAsync
        (ITelegramBotClient bot, string path, long chatId)
    {
        string[] filesArray = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);

        if (filesArray.Length == 0)
        {
            await bot.SendTextMessageAsync(chatId, "Your folder's empty :(");
            return;
        }

        for (int i = 0; i < filesArray.Length; i++)
            filesArray[i] = Path.GetFileNameWithoutExtension(filesArray[i]);

        var iButtonsArrayList = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < filesArray.Length; i++)
        {
            var iButtonsList = new List<InlineKeyboardButton>()
            {
                InlineKeyboardButton.WithCallbackData(filesArray[i])
            };
            iButtonsArrayList.Add(iButtonsList.ToArray());
        }

        InlineKeyboardMarkup iKeyboard = iButtonsArrayList.ToArray();
        await bot.SendTextMessageAsync(chatId, "Uploaded files:", replyMarkup: iKeyboard);
    }

    static void Main(string[] args)
    {
        Console.WriteLine("Listening to " + bot.GetMeAsync().Result.FirstName + "\n");

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );
        Console.ReadLine();
    }
}