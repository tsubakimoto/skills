using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed record AzureResourceEntry(
    string ResourceTypeKey,
    string DisplayName,
    string Category,
    string OfficialPrefix)
{
    public string NamingToken => OfficialPrefix.TrimEnd('-');
}

internal static partial class AzureResourceAbbreviationsSupport
{
    private static readonly IReadOnlyDictionary<string, string> AliasMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ai search"] = "searchSearchServices",
            ["aks"] = "containerServiceManagedClusters",
            ["aks cluster"] = "containerServiceManagedClusters",
            ["app service plan"] = "webServerFarms",
            ["application gateway"] = "networkApplicationGateways",
            ["azure sql database"] = "sqlServersDatabases",
            ["azure sql server"] = "sqlServers",
            ["container registry"] = "containerRegistryRegistries",
            ["function app"] = "webSitesFunctions",
            ["key vault"] = "keyVaultVaults",
            ["load balancer"] = "networkLoadBalancersExternal",
            ["managed identity"] = "managedIdentityUserAssignedIdentities",
            ["mysql database"] = "dBforMySQLServers",
            ["network security group"] = "networkNetworkSecurityGroups",
            ["postgresql database"] = "dBforPostgreSQLServers",
            ["public ip"] = "networkPublicIPAddresses",
            ["resource group"] = "resourcesResourceGroups",
            ["sql database"] = "sqlServersDatabases",
            ["sql server"] = "sqlServers",
            ["static web app"] = "webStaticSites",
            ["storage account"] = "storageStorageAccounts",
            ["subnet"] = "networkVirtualNetworksSubnets",
            ["virtual machine"] = "computeVirtualMachines",
            ["virtual machine scale set"] = "computeVirtualMachineScaleSets",
            ["virtual network"] = "networkVirtualNetworks",
            ["vpn gateway"] = "networkVpnGateways",
            ["web app"] = "webSitesAppService"
        };

    private static readonly IReadOnlyDictionary<string, string> DisplayNameOverrides =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["apiManagementService"] = "API Management Service",
            ["aiFoundryAccounts"] = "AI Foundry Account",
            ["appContainerApps"] = "Container App",
            ["appManagedEnvironments"] = "Container Apps Environment",
            ["cacheRedis"] = "Azure Cache for Redis",
            ["cdnProfiles"] = "CDN Profile",
            ["cdnProfilesEndpoints"] = "CDN Endpoint",
            ["computeVirtualMachines"] = "Virtual Machine",
            ["computeVirtualMachineScaleSets"] = "Virtual Machine Scale Set",
            ["containerRegistryRegistries"] = "Container Registry",
            ["containerServiceManagedClusters"] = "AKS Cluster",
            ["dBforMySQLServers"] = "MySQL Database",
            ["dBforPostgreSQLServers"] = "PostgreSQL Database",
            ["documentDBDatabaseAccounts"] = "Azure Cosmos DB Account",
            ["keyVaultVaults"] = "Key Vault",
            ["managedIdentityUserAssignedIdentities"] = "Managed Identity",
            ["networkApplicationGateways"] = "Application Gateway",
            ["networkApplicationSecurityGroups"] = "Application Security Group",
            ["networkAzureFirewalls"] = "Firewall",
            ["networkDnsZones"] = "DNS Zone",
            ["networkExpressRouteCircuits"] = "ExpressRoute Circuit",
            ["networkFirewallPolicies"] = "Firewall Policy",
            ["networkFrontDoors"] = "Front Door",
            ["networkFrontdoorWebApplicationFirewallPolicies"] = "Front Door Web Application Firewall Policy",
            ["networkLoadBalancersExternal"] = "Load Balancer (External)",
            ["networkLoadBalancersInboundNatRules"] = "Load Balancer Inbound NAT Rule",
            ["networkLoadBalancersInternal"] = "Load Balancer (Internal)",
            ["networkLocalNetworkGateways"] = "Local Network Gateway",
            ["networkNatGateways"] = "NAT Gateway",
            ["networkNetworkInterfaces"] = "Network Interface",
            ["networkNetworkSecurityGroups"] = "Network Security Group",
            ["networkNetworkSecurityGroupsSecurityRules"] = "Network Security Group Security Rule",
            ["networkNetworkWatchers"] = "Network Watcher",
            ["networkPrivateDnsZones"] = "Private DNS Zone",
            ["networkPrivateLinkServices"] = "Private Link Service",
            ["networkPublicIPAddresses"] = "Public IP Address",
            ["networkPublicIPPrefixes"] = "Public IP Prefix",
            ["networkRouteFilters"] = "Route Filter",
            ["networkRouteTables"] = "Route Table",
            ["networkRouteTablesRoutes"] = "User Defined Route",
            ["networkTrafficManagerProfiles"] = "Traffic Manager Profile",
            ["networkVirtualNetworkGateways"] = "Virtual Network Gateway",
            ["networkVirtualNetworks"] = "Virtual Network",
            ["networkVirtualNetworksVirtualNetworkPeerings"] = "Virtual Network Peering",
            ["networkVirtualNetworksSubnets"] = "Subnet",
            ["networkVirtualWans"] = "Virtual WAN",
            ["operationalInsightsWorkspaces"] = "Log Analytics Workspace",
            ["powerBIDedicatedCapacities"] = "Power BI Dedicated Capacity",
            ["resourcesResourceGroups"] = "Resource Group",
            ["searchSearchServices"] = "AI Search",
            ["serviceEndPointPolicies"] = "Service Endpoint Policy",
            ["serviceBusNamespaces"] = "Service Bus Namespace",
            ["signalRServiceSignalR"] = "SignalR",
            ["sqlServers"] = "Azure SQL Database Server",
            ["sqlServersDatabases"] = "Azure SQL Database",
            ["storageStorageAccounts"] = "Storage Account",
            ["webServerFarms"] = "App Service Plan",
            ["webSitesAppService"] = "Web App",
            ["webSitesFunctions"] = "Function App",
            ["webStaticSites"] = "Static Web App"
        };

    private static readonly (string Prefix, string Category)[] CategoryMappings =
    [
        ("aiFoundry", "AI + Machine Learning"),
        ("cognitiveServices", "AI + Machine Learning"),
        ("machineLearningServices", "AI + Machine Learning"),
        ("searchSearch", "AI + Machine Learning"),
        ("analysisServices", "Analytics and IoT"),
        ("databricks", "Analytics and IoT"),
        ("dataFactory", "Analytics and IoT"),
        ("dataLake", "Analytics and IoT"),
        ("devices", "Analytics and IoT"),
        ("eventGrid", "Analytics and IoT"),
        ("eventHub", "Analytics and IoT"),
        ("hdInsight", "Analytics and IoT"),
        ("kusto", "Analytics and IoT"),
        ("powerBI", "Analytics and IoT"),
        ("streamAnalytics", "Analytics and IoT"),
        ("synapse", "Analytics and IoT"),
        ("timeSeriesInsights", "Analytics and IoT"),
        ("appConfiguration", "Developer Tools"),
        ("signalRService", "Developer Tools"),
        ("appContainerApps", "Containers"),
        ("appManagedEnvironments", "Containers"),
        ("containerInstance", "Containers"),
        ("containerRegistry", "Containers"),
        ("containerService", "Containers"),
        ("kubernetesConnectedClusters", "Containers"),
        ("serviceFabric", "Containers"),
        ("cacheRedis", "Databases"),
        ("dBforMySQL", "Databases"),
        ("dBforPostgreSQL", "Databases"),
        ("documentDB", "Databases"),
        ("sql", "Databases"),
        ("apiManagement", "Integration"),
        ("logic", "Integration"),
        ("serviceBus", "Integration"),
        ("authorization", "Management and Governance"),
        ("automation", "Management and Governance"),
        ("blueprint", "Management and Governance"),
        ("insights", "Management and Governance"),
        ("managementManagementGroups", "Management and Governance"),
        ("operationalInsights", "Management and Governance"),
        ("portalDashboards", "Management and Governance"),
        ("purview", "Management and Governance"),
        ("resourcesResourceGroups", "Management and Governance"),
        ("dataMigration", "Migration"),
        ("migrateAssessmentProjects", "Migration"),
        ("recoveryServices", "Migration"),
        ("keyVault", "Security"),
        ("managedIdentity", "Security"),
        ("networkBastion", "Security"),
        ("networkFirewallPoliciesRuleGroups", "Security"),
        ("networkFirewallPoliciesWebApplication", "Security"),
        ("networkVpn", "Security"),
        ("network", "Networking"),
        ("serviceEndPointPolicies", "Networking"),
        ("cdnProfiles", "Networking"),
        ("compute", "Compute and Web"),
        ("hybridCompute", "Compute and Web"),
        ("notificationHubs", "Compute and Web"),
        ("webServerFarms", "Compute and Web"),
        ("webSites", "Compute and Web"),
        ("webStaticSites", "Compute and Web"),
        ("storage", "Storage"),
        ("storSimpleManagers", "Storage")
    ];

    private static readonly string[] CategoryOrder =
    [
        "AI + Machine Learning",
        "Analytics and IoT",
        "Compute and Web",
        "Containers",
        "Databases",
        "Developer Tools",
        "Integration",
        "Management and Governance",
        "Migration",
        "Networking",
        "Security",
        "Storage"
    ];

    private static readonly IReadOnlyList<AzureResourceEntry> Entries = LoadEntries();

    public static IReadOnlyList<string> GetCategories() =>
        CategoryOrder.Where(category => Entries.Any(entry => entry.Category == category)).ToArray();

    public static AzureResourceEntry? FindExact(string query)
    {
        var normalizedQuery = Normalize(query);
        if (AliasMap.TryGetValue(query.Trim(), out var aliasKey))
        {
            return Entries.FirstOrDefault(entry => entry.ResourceTypeKey == aliasKey);
        }

        return Entries.FirstOrDefault(entry =>
            Normalize(entry.ResourceTypeKey) == normalizedQuery ||
            Normalize(entry.DisplayName) == normalizedQuery ||
            Normalize(entry.NamingToken) == normalizedQuery ||
            Normalize(entry.OfficialPrefix) == normalizedQuery);
    }

    public static IReadOnlyList<AzureResourceEntry> Search(string query)
    {
        var normalizedQuery = Normalize(query);
        if (AliasMap.TryGetValue(query.Trim(), out var aliasKey))
        {
            var aliasMatch = Entries.FirstOrDefault(entry => entry.ResourceTypeKey == aliasKey);
            if (aliasMatch is not null)
            {
                return
                [
                    aliasMatch,
                    ..Entries.Where(entry => entry.ResourceTypeKey != aliasKey &&
                        (Normalize(entry.ResourceTypeKey).Contains(normalizedQuery, StringComparison.Ordinal) ||
                         Normalize(entry.DisplayName).Contains(normalizedQuery, StringComparison.Ordinal) ||
                         Normalize(entry.Category).Contains(normalizedQuery, StringComparison.Ordinal) ||
                         Normalize(entry.NamingToken).Contains(normalizedQuery, StringComparison.Ordinal) ||
                         Normalize(entry.OfficialPrefix).Contains(normalizedQuery, StringComparison.Ordinal)))
                        .OrderBy(entry => entry.Category, StringComparer.Ordinal)
                        .ThenBy(entry => entry.DisplayName, StringComparer.Ordinal)
                ];
            }
        }

        return Entries
            .Where(entry =>
                Normalize(entry.ResourceTypeKey).Contains(normalizedQuery, StringComparison.Ordinal) ||
                Normalize(entry.DisplayName).Contains(normalizedQuery, StringComparison.Ordinal) ||
                Normalize(entry.Category).Contains(normalizedQuery, StringComparison.Ordinal) ||
                Normalize(entry.NamingToken).Contains(normalizedQuery, StringComparison.Ordinal) ||
                Normalize(entry.OfficialPrefix).Contains(normalizedQuery, StringComparison.Ordinal))
            .OrderBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<AzureResourceEntry> GetCategoryEntries(string category) =>
        Entries
            .Where(entry => entry.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ToArray();

    public static string GetReferencePath() => ReferencePath;

    private static IReadOnlyList<AzureResourceEntry> LoadEntries()
    {
        var json = File.ReadAllText(ReferencePath);
        var resourceMap =
            JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
            throw new InvalidOperationException("references\\abbreviations.json could not be parsed.");

        return resourceMap
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AzureResourceEntry(
                pair.Key,
                GetDisplayName(pair.Key),
                GetCategory(pair.Key),
                pair.Value))
            .ToArray();
    }

    private static string ReferencePath => Path.GetFullPath(
        Path.Combine(ScriptDirectory, "..", "references", "abbreviations.json"));

    private static string ScriptDirectory => Path.GetDirectoryName(GetThisFilePath())!;

    private static string GetThisFilePath([CallerFilePath] string path = "") => path;

    private static string GetDisplayName(string resourceTypeKey)
    {
        if (DisplayNameOverrides.TryGetValue(resourceTypeKey, out var displayName))
        {
            return displayName;
        }

        var tokens = SplitCamelCaseRegex().Split(resourceTypeKey);
        return string.Join(
            " ",
            tokens
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Select(FormatToken));
    }

    private static string GetCategory(string resourceTypeKey)
    {
        foreach (var (prefix, category) in CategoryMappings)
        {
            if (resourceTypeKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                return category;
            }
        }

        return "Other";
    }

    private static string Normalize(string value) =>
        Regex.Replace(value, "[^a-z0-9]+", string.Empty, RegexOptions.IgnoreCase).ToLowerInvariant();

    private static string FormatToken(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "AI" => "AI",
            "AKS" => "AKS",
            "API" => "API",
            "BI" => "BI",
            "CDN" => "CDN",
            "DB" => "DB",
            "DNS" => "DNS",
            "HD" => "HD",
            "HSM" => "HSM",
            "IP" => "IP",
            "IOT" => "IoT",
            "NAT" => "NAT",
            "SQL" => "SQL",
            "SSH" => "SSH",
            "VPN" => "VPN",
            "VM" => "VM",
            _ => char.ToUpperInvariant(token[0]) + token[1..]
        };
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex SplitCamelCaseRegex();
}
