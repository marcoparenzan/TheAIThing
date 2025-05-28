using System;
using System.Collections.Generic;
using System.Text;

namespace AIAppLib;

public class AzureOpenAICredentials
{
    public string DeploymentName { get; set; }
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }
}
