using Octokit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

class Program
{
    static string? owner;
    static string? repo;
    static string? directoryPath;
    static int checkInTime;
    static string? branch;
    static System.Timers.Timer? timer;

    static async Task Main(string[] args)
    {
        var isService = args.Contains("--service");

        // Read the .config file and start the timer
        await CheckForUpdates(isService);

        // If the application is running as a service, prevent it from exiting
        if (isService)
        {
            await Task.Delay(-1);
        }
    }

    static async Task CheckForUpdates(bool isService)
    {
        try
        {
            // Set the name of the configuration file
            var configFileName = "configuration.txt";

            // Check if the configuration file exists
            if (!File.Exists(configFileName))
            {
                // Create the configuration file with empty values
                File.WriteAllLines(configFileName, new string[]
                {
                    "owner=\"\"",
                    "repo=\"\"",
                    "directoryPath=\"\"",
                    "checkInTime=\"\"",
                    "branch=\"\""
                });

                LogError($"The {configFileName} file has been created. Please fill in the values.");
                return;
            }

            // Read the configuration file
            var lines = File.ReadAllLines(configFileName);

            // Parse the configuration file
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length != 2)
                {
                    LogError($"The {configFileName} file is not formatted correctly.");
                    return;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');

                switch (key)
                {
                    case "owner":
                        owner = value;
                        break;
                    case "repo":
                        repo = value;
                        break;
                    case "directoryPath":
                        directoryPath = value;
                        break;
                    case "checkInTime":
                        if (!int.TryParse(value, out checkInTime))
                        {
                            LogError("The check-in time in the configuration file is not a valid integer.");
                            return;
                        }
                        break;
                    case "branch":
                        branch = value;
                        break;
                    default:
                        LogError($"Unknown key in {configFileName}: {key}");
                        return;
                }
            }

            // Ensure checkInTime is not less than 1 minute
            if (checkInTime < 1)
            {
                LogError("Check-in time cannot be less than 1 minute.");
                return;
            }

            // Initialize and start the timer
            timer = new System.Timers.Timer(checkInTime * 60000);
            timer.Elapsed += async (sender, e) => await CheckForUpdates(isService);
            timer.Start();

            var client = new GitHubClient(new ProductHeaderValue("MyApp"));
            var commits = await client.Repository.Commit.GetAll(owner, repo);
            var latestCommit = commits[0].Sha;

            var localCommitPath = "latest_commit.txt";
            var localCommit = File.Exists(localCommitPath) ? File.ReadAllText(localCommitPath) : null;

            if (localCommit != latestCommit)
            {
                // Initialize the directory as a git repository if it's not already one
                if (!Directory.Exists(Path.Combine(directoryPath, ".git")))
                {
                    RunCommand("init", directoryPath, isService);
                    RunCommand($"remote add origin https://github.com/{owner}/{repo}.git", directoryPath, isService);
                    RunCommand($"pull origin {branch}", directoryPath, isService);
                }
                else
                {
                    // Pull the changes
                    RunCommand($"pull origin {branch}", directoryPath, isService);
                }

                // Update the local commit
                File.WriteAllText(localCommitPath, latestCommit);

                // Ensure the logs directory exists
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));

                // Log the successful update
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "updates_log.txt"), DateTime.Now + ": Successfully updated to commit " + latestCommit + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Log the error
            LogError(ex.Message);
        }
    }

    static void RunCommand(string command, string workingDirectory, bool isService)
    {
        var gitPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "git", "bin", "git.exe");

        // Check if the git directory exists
        if (!File.Exists(gitPath))
        {
            LogError("The git directory does not exist in the application's directory.");
            return;
        }

        var process = new System.Diagnostics.Process();
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            WindowStyle = isService ? System.Diagnostics.ProcessWindowStyle.Hidden : System.Diagnostics.ProcessWindowStyle.Normal,
            FileName = gitPath,
            WorkingDirectory = workingDirectory,
            Arguments = command
        };
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }

    static void LogError(string message)
    {
        // Ensure the logs directory exists
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));

        // Log the error
        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "errors_log.txt"), DateTime.Now + ": An error occurred: " + message + Environment.NewLine);
    }
}
