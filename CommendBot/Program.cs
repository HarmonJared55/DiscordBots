using System.Reflection;
using CommendBod.Model;
using CommendBot.Data;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Load configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .Build();

var token = config["Discord:Token"]
    ?? throw new InvalidOperationException("Bot token not found. Set 'Discord:Token' in appsettings.json.");

var guildIdString = config["Discord:GuildId"]
    ?? throw new InvalidOperationException("Guild ID not found. Set 'Discord:GuildId' in appsettings.json.");

var guildId = ulong.Parse(guildIdString);

// Configure Entity Framework with SQL Server
var connectionString = config.GetConnectionString("CommendBotDb")
    ?? throw new InvalidOperationException("Connection string 'CommendBotDb' not found in appsettings.json.");

var dbOptions = new DbContextOptionsBuilder<CommendBotDbContext>()
    .UseSqlServer(connectionString)
    .Options;

// Ensure the database and tables are created
using (var initContext = new CommendBotDbContext(dbOptions))
{
    await initContext.Database.MigrateAsync();
    Console.WriteLine("Database migrated and connection established.");
}

// Configure the Discord client
var discordConfig = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
};

var client = new DiscordSocketClient(discordConfig);

client.Log += LogAsync;
client.Ready += ReadyAsync;
client.SlashCommandExecuted += SlashCommandExecutedAsync;

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

// Block the program until it is closed
await Task.Delay(Timeout.Infinite);

// --- Event Handlers ---

Task LogAsync(LogMessage msg)
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
}

async Task ReadyAsync()
{
    Console.WriteLine($"Bot connected as {client.CurrentUser.Username}");

    // Register slash commands to a specific guild (instant availability)
    var guild = client.GetGuild(guildId);
    if (guild is null)
    {
        Console.WriteLine($"Guild {guildId} not found. Make sure the bot has been invited to that server.");
        return;
    }

    List<SlashCommandBuilder> slashCommandBuilders =
    [
        new SlashCommandBuilder()
            .WithName("ping")
            .WithDescription("Replies with Pong!"),

        new SlashCommandBuilder()
            .WithName("commend")
            .WithDescription("Commend a user for valor on the battlefield")
            .AddOption("user", ApplicationCommandOptionType.User, "The user to commend", isRequired: true)
            .AddOption("reason", ApplicationCommandOptionType.String, "The reason for the commendation", isRequired: false)
            .AddOption("role", ApplicationCommandOptionType.Role, "The role to assign for this commendation", isRequired: false),

        new SlashCommandBuilder()
            .WithName("leaderboard")
            .WithDescription("Display the top 5 most commended users")
    ];

    foreach (var commandBuilder in slashCommandBuilders)
    {
        try
        {
            await guild.CreateApplicationCommandAsync(commandBuilder.Build());
            Console.WriteLine($"Registered /'{commandBuilder.Name}' command in guild '{guild.Name}'.");
        }
        catch (HttpException ex)
        {
            Console.WriteLine($"Failed to register command: {ex.Message}");
        }
    }
}

// --- Slash Command Handlers ---

async Task SlashCommandExecutedAsync(SocketSlashCommand command)
{
    switch (command.CommandName)
    {
        case "ping":
            await HandlePingCommandAsync(command);
            break;
        case "commend":
            await HandleCommendCommandAsync(command);
            break;
        case "leaderboard":
            await HandleLeaderboardCommandAsync(command);
            break;
    }
}

async Task HandlePingCommandAsync(SocketSlashCommand command)
{
    await command.RespondAsync("Pong! 🏓");
}

async Task HandleCommendCommandAsync(SocketSlashCommand command)
{
    var userOption = command.Data.Options.FirstOrDefault(opt => opt.Name == "user");
    var reasonOption = command.Data.Options.FirstOrDefault(opt => opt.Name == "reason");
    var roleOption = command.Data.Options.FirstOrDefault(opt => opt.Name == "role");

    if (userOption is null)
    {
        await command.RespondAsync("Error: User option is required.", ephemeral: true);
        return;
    }

    var user = (SocketGuildUser)userOption.Value;
    var reason = reasonOption?.Value as string ?? "No reason provided.";
    var role = roleOption?.Value as SocketRole;

    // Save the commendation to the database
    try
    {
        using var dbContext = new CommendBotDbContext(dbOptions);
        var repository = new CommendRepository(dbContext);

        var commend = new Commend
        {
            UserId = user.Id,
            Reason = reason,
            SentByUserId = command.User.Id,
            CreatedDate = DateTime.UtcNow
        };

        await repository.CreateAsync(commend);

        string responseMessage = $"Commended {user.Mention} for valor on the battlefield performing their role of {role?.Name}! Reason: {reason}";
        //if (role != null)
        //{
        //    responseMessage += $" Assigned role: {role.Mention}";
        //    await user.AddRoleAsync(role);
        //}
        await command.RespondAsync(responseMessage);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving commend: {ex.Message}");
        await command.RespondAsync("An error occurred while saving the commendation. Please try again later.", ephemeral: true);
    }
}

async Task HandleLeaderboardCommandAsync(SocketSlashCommand command)
{
    try
    {
        using var dbContext = new CommendBotDbContext(dbOptions);
        var repository = new CommendRepository(dbContext);

        var topUsers = await repository.GetTopCommendedUsersAsync(5);

        if (topUsers.Count == 0)
        {
            await command.RespondAsync("No commendations have been recorded yet. Be the first to commend someone!");
            return;
        }

        var guild = client.GetGuild(guildId);

        var embed = new EmbedBuilder()
            .WithTitle("🏆 Commendation Leaderboard")
            .WithDescription("Top 5 most commended users on the battlefield!")
            .WithColor(Color.Gold)
            .WithTimestamp(DateTimeOffset.UtcNow);

        for (int i = 0; i < topUsers.Count; i++)
        {
            var (userId, commendCount) = topUsers[i];
            string medal = i switch
            {
                0 => "🥇",
                1 => "🥈",
                2 => "🥉",
                _ => $"**#{i + 1}**"
            };

            var guildUser = guild?.GetUser(userId);
            string displayName = guildUser?.DisplayName ?? $"Unknown User ({userId})";

            embed.AddField(
                $"{medal} {displayName}",
                $"{commendCount} commend{(commendCount != 1 ? "s" : "")}",
                inline: false);
        }

        await command.RespondAsync(embed: embed.Build());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error fetching leaderboard: {ex.Message}");
        await command.RespondAsync("An error occurred while fetching the leaderboard. Please try again later.", ephemeral: true);
    }
}