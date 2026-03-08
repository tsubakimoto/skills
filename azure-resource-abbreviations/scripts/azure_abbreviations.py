"""
Azure Resource Abbreviations Database
Reference: https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
"""

# Azure resource abbreviations organized by category
AZURE_ABBREVIATIONS = {
    "AI + Machine Learning": {
        "AI Search": "srch",
        "Foundry Tools (Multi-service Account)": "ais",
        "Foundry Account": "aif",
        "Foundry Account Project": "proj",
        "Foundry Hub": "hub",
        "Foundry Hub Project": "proj",
        "Azure AI Video Indexer": "avi",
        "Azure Machine Learning Workspace": "mlw",
        "Azure OpenAI Service": "oai",
        "Bot Service": "bot",
        "Computer Vision": "cv",
        "Content Moderator": "cm",
        "Content Safety": "cs",
        "Custom Vision (Prediction)": "cstv",
        "Custom Vision (Training)": "cstvt",
        "Document Intelligence": "di",
        "Face API": "face",
        "Health Insights": "hi",
        "Immersive Reader": "ir",
        "Language Service": "lang",
        "Speech Service": "spch",
        "Translator": "trsl",
    },
    "Analytics and IoT": {
        "Azure Analysis Services Server": "as",
        "Azure Databricks Access Connector": "dbac",
        "Azure Databricks Workspace": "dbw",
        "Azure Data Explorer Cluster": "dec",
        "Azure Data Explorer Cluster Database": "dedb",
        "Azure Data Factory": "adf",
        "Azure Digital Twins Instance": "dt",
        "Azure Stream Analytics": "asa",
        "Azure Synapse Analytics Private Link Hub": "synplh",
        "Azure Synapse Analytics SQL Dedicated Pool": "syndp",
        "Azure Synapse Analytics Spark Pool": "synsp",
        "Azure Synapse Analytics Workspace": "synw",
        "Data Lake Store Account": "dls",
        "Event Hubs Namespace": "evhns",
        "Event Hub": "evh",
        "Event Grid Domain": "evgd",
        "Event Grid Namespace": "evgns",
        "Event Grid Subscription": "evgs",
        "Event Grid Topic": "evgt",
        "Event Grid System Topic": "egst",
        "Fabric Capacity": "fc",
        "HDInsight - Hadoop Cluster": "hadoop",
        "HDInsight - HBase Cluster": "hbase",
        "HDInsight - Kafka Cluster": "kafka",
        "HDInsight - Spark Cluster": "spark",
        "HDInsight - Storm Cluster": "storm",
        "HDInsight - ML Services Cluster": "mls",
        "IoT Hub": "iot",
        "Provisioning Service": "provs",
        "Provisioning Service Certificate": "pcert",
        "Power BI Embedded": "pbi",
        "Time Series Insights Environment": "tsi",
    },
    "Compute and Web": {
        "App Service Environment": "ase",
        "App Service Plan": "asp",
        "Azure Load Testing Instance": "lt",
        "Availability Set": "avail",
        "Azure Arc Enabled Server": "arcs",
        "Azure Arc Enabled Kubernetes Cluster": "arck",
        "Azure Arc Private Link Scope": "pls",
        "Azure Arc Gateway": "arcgw",
        "Batch Account": "ba",
        "Cloud Service": "cld",
        "Communication Services": "acs",
        "Disk Encryption Set": "des",
        "Function App": "func",
        "Gallery": "gal",
        "Hosting Environment": "host",
        "Image Template": "it",
        "Managed Disk (OS)": "osdisk",
        "Managed Disk (Data)": "disk",
        "Notification Hubs": "ntf",
        "Notification Hubs Namespace": "ntfns",
        "Proximity Placement Group": "ppg",
        "Restore Point Collection": "rpc",
        "Snapshot": "snap",
        "Static Web App": "stapp",
        "Virtual Machine": "vm",
        "Virtual Machine Scale Set": "vmss",
        "Virtual Machine Maintenance Configuration": "mc",
        "VM Storage Account": "stvm",
        "Web App": "app",
    },
    "Containers": {
        "AKS Cluster": "aks",
        "AKS System Node Pool": "npsystem",
        "AKS User Node Pool": "np",
        "Container App": "ca",
        "Container Apps Environment": "cae",
        "Container App Job": "caj",
        "Container Registry": "cr",
        "Container Instance": "ci",
        "Service Fabric Cluster": "sf",
        "Service Fabric Managed Cluster": "sfmc",
    },
    "Databases": {
        "Azure Cosmos DB Database": "cosmos",
        "Azure Cosmos DB for Apache Cassandra Account": "coscas",
        "Azure Cosmos DB for MongoDB Account": "cosmon",
        "Azure Cosmos DB for NoSQL Account": "cosno",
        "Azure Cosmos DB for Table Account": "costab",
        "Azure Cosmos DB for Apache Gremlin Account": "cosgrm",
        "Azure Cosmos DB PostgreSQL Cluster": "cospos",
        "Azure Cache for Redis Instance": "redis",
        "Azure Managed Redis": "amr",
        "Azure SQL Database Server": "sql",
        "Azure SQL Database": "sqldb",
        "Azure SQL Elastic Job Agent": "sqlja",
        "Azure SQL Elastic Pool": "sqlep",
        "MySQL Database": "mysql",
        "PostgreSQL Database": "psql",
        "SQL Managed Instance": "sqlmi",
    },
    "Developer Tools": {
        "App Configuration Store": "appcs",
        "Maps Account": "map",
        "SignalR": "sigr",
        "WebPubSub": "wps",
    },
    "DevOps": {
        "Azure Managed Grafana": "amg",
        "Managed DevOps Pool": "mdp",
    },
    "Integration": {
        "API Management Service Instance": "apim",
        "Integration Account": "ia",
        "Logic App": "logic",
        "Service Bus Namespace": "sbns",
        "Service Bus Queue": "sbq",
        "Service Bus Topic": "sbt",
        "Service Bus Topic Subscription": "sbts",
    },
    "Management and Governance": {
        "Automation Account": "aa",
        "Azure Policy Definition": "<description>",
        "Application Insights": "appi",
        "Azure Monitor Action Group": "ag",
        "Azure Monitor Data Collection Rule": "dcr",
        "Azure Monitor Alert Processing Rule": "apr",
        "Data Collection Endpoint": "dce",
        "Deployment Script": "script",
        "Log Analytics Workspace": "log",
        "Log Analytics Query Pack": "pack",
        "Management Group": "mg",
        "Microsoft Purview Instance": "pview",
        "Resource Group": "rg",
        "Template Spec Name": "ts",
    },
    "Migration": {
        "Azure Migrate Project": "migr",
        "Database Migration Service Instance": "dms",
        "Recovery Services Vault": "rsv",
    },
    "Networking": {
        "Application Gateway": "agw",
        "Application Security Group": "asg",
        "CDN Profile": "cdnp",
        "CDN Endpoint": "cdne",
        "Connections": "con",
        "DNS": "dns",
        "DNS Forwarding Ruleset": "dnsfrs",
        "DNS Private Resolver": "dnspr",
        "DNS Private Resolver Inbound Endpoint": "in",
        "DNS Private Resolver Outbound Endpoint": "out",
        "DNS Zone": "dns",
        "Firewall": "afw",
        "Firewall Policy": "afwp",
        "ExpressRoute Circuit": "erc",
        "ExpressRoute Direct": "erd",
        "ExpressRoute Gateway": "ergw",
        "Front Door Profile": "afd",
        "Front Door Endpoint": "fde",
        "Front Door Firewall Policy": "fdfp",
        "IP Group": "ipg",
        "Load Balancer (Internal)": "lbi",
        "Load Balancer (External)": "lbe",
        "Load Balancer Rule": "rule",
        "Local Network Gateway": "lgw",
        "NAT Gateway": "ng",
        "Network Interface": "nic",
        "Network Security Perimeter": "nsp",
        "Network Security Group": "nsg",
        "Network Security Group Security Rule": "nsgsr",
        "Network Watcher": "nw",
        "Private Link": "pl",
        "Private Endpoint": "pep",
        "Public IP Address": "pip",
        "Public IP Address Prefix": "ippre",
        "Route Filter": "rf",
        "Route Server": "rtserv",
        "Route Table": "rt",
        "Service Endpoint Policy": "se",
        "Traffic Manager Profile": "traf",
        "User Defined Route": "udr",
        "Virtual Network": "vnet",
        "Virtual Network Gateway": "vgw",
        "Virtual Network Manager": "vnm",
        "Virtual Network Peering": "peer",
        "Virtual Network Subnet": "snet",
        "Virtual WAN": "vwan",
        "Virtual WAN Hub": "vhub",
    },
    "Security": {
        "Azure Bastion": "bas",
        "Key Vault": "kv",
        "Key Vault Managed HSM": "kvmhsm",
        "Managed Identity": "id",
        "SSH Key": "sshkey",
        "VPN Gateway": "vpng",
        "VPN Connection": "vcn",
        "VPN Site": "vst",
        "Web Application Firewall Policy": "waf",
        "Web Application Firewall Policy Rule Group": "wafrg",
    },
    "Storage": {
        "Backup Vault": "bvault",
        "Backup Vault Policy": "bkpol",
        "File Share": "share",
        "Storage Account": "st",
        "Storage Sync Service": "sss",
    },
    "Virtual Desktop Infrastructure": {
        "Virtual Desktop Host Pool": "vdpool",
        "Virtual Desktop Application Group": "vdag",
        "Virtual Desktop Workspace": "vdws",
        "Virtual Desktop Scaling Plan": "vdscaling",
    },
}


def get_abbreviation(resource_name: str) -> str:
    """Get abbreviation for a resource type."""
    for category, resources in AZURE_ABBREVIATIONS.items():
        if resource_name in resources:
            return resources[resource_name]
    return None


def search_abbreviation(query: str) -> dict:
    """Search for abbreviations matching the query (case-insensitive)."""
    results = {}
    query_lower = query.lower()
    
    for category, resources in AZURE_ABBREVIATIONS.items():
        for resource_name, abbreviation in resources.items():
            if query_lower in resource_name.lower():
                if category not in results:
                    results[category] = {}
                results[category][resource_name] = abbreviation
    
    return results


def get_category_resources(category_name: str) -> dict:
    """Get all resources in a category."""
    for category, resources in AZURE_ABBREVIATIONS.items():
        if category_name.lower() == category.lower():
            return resources
    return {}


def get_all_categories() -> list:
    """Get all available categories."""
    return list(AZURE_ABBREVIATIONS.keys())


def get_all_resources() -> dict:
    """Get all resources with their abbreviations."""
    return AZURE_ABBREVIATIONS


def validate_naming_convention(resource_name: str, abbreviation: str) -> bool:
    """Validate if a resource name uses a valid abbreviation."""
    found = get_abbreviation(resource_name)
    return found is not None and found == abbreviation


if __name__ == "__main__":
    # Test examples
    print("Sample abbreviations:")
    print(f"Virtual Machine: {get_abbreviation('Virtual Machine')}")
    print(f"Web App: {get_abbreviation('Web App')}")
    print(f"Virtual Network: {get_abbreviation('Virtual Network')}")
    print(f"Storage Account: {get_abbreviation('Storage Account')}")
    print()
    print("Available categories:")
    for cat in get_all_categories():
        print(f"  - {cat}")
