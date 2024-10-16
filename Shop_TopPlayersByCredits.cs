using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using ShopAPI;
using MySqlConnector;
using Dapper;
using Microsoft.Extensions.Logging;

namespace TopPlayersByCredits;

public class TopPlayersByCredits : BasePlugin
{
    public override string ModuleName => "[SHOP] Top players by credits";
    public override string ModuleAuthor => "ABKAM";
    public override string ModuleVersion => "1.0";

    private IShopApi? _api;
    private TopPlayersConfig? _config;
    private string? _configPath;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/TopCredits.json");
        
        LoadConfig();
        
        _api = IShopApi.Capability.Get();
        if (_api == null) throw new Exception("SHOP CORE NOT LOADED!!!");

        _api.AddToFunctionsMenu(_api.GetTranslatedText(null, "Menu_ShowTopPlayers"), ShowTopPlayersMenu);
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            _config = new TopPlayersConfig();
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath!, json);
        }
        else
        {
            string json = File.ReadAllText(_configPath);
            _config = JsonSerializer.Deserialize<TopPlayersConfig>(json) ?? new TopPlayersConfig();
        }
        
        if (_config.TopPlayersCount <= 0)
        {
            _config.TopPlayersCount = 10;
        }
    }
    
    private void ShowTopPlayersMenu(CCSPlayerController player)
    {
        Task.Run(async () =>
        {
            var topPlayers = await GetTopPlayersAsync();
            
            Server.NextFrame(() =>
            {
                var menuTitle = _api!.GetTranslatedText(player, "Menu_TopPlayersTitle", _config!.TopPlayersCount);
                var menu = _api.CreateMenu(menuTitle);

                if (topPlayers.Count == 0)
                {
                    menu.AddMenuOption(_api.GetTranslatedText(player, "Menu_NoDataToDisplay"), null!, true);
                }
                else
                {
                    int rank = 1;
                    foreach (var (playerName, credits) in topPlayers)
                    {
                        menu.AddMenuOption(_api.GetTranslatedText(player, "Menu_TopPlayerEntry", rank, playerName, credits), null!, true);
                        rank++;
                    }
                }
                
                menu.Open(player);
            });
        });
    }
    
    private async Task<List<(string PlayerName, int Credits)>> GetTopPlayersAsync()
    {
        var topPlayers = new List<(string PlayerName, int Credits)>();

        try
        {
            await using (var connection = new MySqlConnection(_api!.dbConnectionString))
            {
                await connection.OpenAsync();
                
                var query = $"SELECT `name` AS PlayerName, `money` AS Credits FROM `shop_players` ORDER BY `money` DESC LIMIT {_config!.TopPlayersCount};";
                var result = await connection.QueryAsync<(string PlayerName, int Credits)>(query);

                topPlayers = result.ToList();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error retrieving top players: {ex.Message}");
        }

        return topPlayers;
    }
}

public class TopPlayersConfig
{
    public int TopPlayersCount { get; set; } = 10;
}
