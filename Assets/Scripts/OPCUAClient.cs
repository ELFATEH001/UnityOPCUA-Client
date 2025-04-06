using UnityEngine;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using UnityEditor;

[System.Serializable]
public class TagValue
{
    public string DisplayName; // Name of the tag (e.g., "g_tick1")
    public string Value;       // Current value of the tag
    public string SourceTimestamp; // Timestamp of the value
    public BuiltInType DataType;   // Data type of the tag (e.g., Int32, Boolean, Float)

    public void UpdateValue(string newValue)
    {
        Value = newValue;
    }
}

public class OPCUAClient : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "opc.tcp://192.168.1.2:4840";

    [SerializeField]
    public List<TagValue> tagValues = new List<TagValue>();

    private Session session;
    private Subscription subscription;
    private Queue<Action> mainThreadActions = new Queue<Action>();

    // Event to notify subscribers when a tag value is updated
    public delegate void TagValueUpdatedHandler(TagValue updatedTag);
    public event TagValueUpdatedHandler OnTagValueUpdated;

    private void Start()
    {
        _ = StartAsync(); // Fire and forget
    }

    private async Task StartAsync()
    {
        // Add tags to monitor
        tagValues.Add(new TagValue { DisplayName = "System_State", DataType = BuiltInType.String });
        tagValues.Add(new TagValue { DisplayName = "DriveX.fActPosition", DataType = BuiltInType.Double });
        tagValues.Add(new TagValue { DisplayName = "DriveY.fActPosition", DataType = BuiltInType.Double });
        tagValues.Add(new TagValue { DisplayName = "DriveZ.fActPosition", DataType = BuiltInType.Double });
        tagValues.Add(new TagValue { DisplayName = "X_postion", DataType = BuiltInType.Double });
        tagValues.Add(new TagValue { DisplayName = "Y_postion", DataType = BuiltInType.Double });
        tagValues.Add(new TagValue { DisplayName = "Z_postion", DataType = BuiltInType.Double });
        tagValues.Add(new TagValue { DisplayName = "Power_system", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "ShutOff_system", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Excute_Movement", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "MoveToIDLE", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Auto_Mode", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Manuel_Mode", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Set_Manuel_Values", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Open_Gripper", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "ResetGroup", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Sen1_Box_detected", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Sen2_Box_picked", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Sen3_Ready_to_Color_detction", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Sen4_Box_placed", DataType = BuiltInType.Boolean });
        tagValues.Add(new TagValue { DisplayName = "Sen5_", DataType = BuiltInType.Boolean });

        var config = new ApplicationConfiguration()
        {
            ApplicationName = "UnityOPCClient",
            ApplicationUri = Opc.Ua.Utils.Format(@"urn:{0}:UnityOPCClient", System.Net.Dns.GetHostName()),
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault",
                    SubjectName = "UnityOPCClient"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications"
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = @"Directory",
                    StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates"
                },
                AutoAcceptUntrustedCertificates = true
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        try
        {
            await config.Validate(ApplicationType.Client);

            var selectedEndpoint = CoreClientUtils.SelectEndpoint(serverUrl, useSecurity: true);
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            session = await Session.Create(
                config,
                endpoint,
                false,
                "UnityOPCClient",
                60000,
                new UserIdentity(),
                null
            );

            Debug.Log($"Connected to OPC UA server at {serverUrl}");
            SetupSubscription();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
            _ = ReconnectAsync(); // Start reconnection process
        }
    }

    private async Task ReconnectAsync()
    {
        while (true)
        {
            try
            {
                if (session == null || !session.Connected)
                {
                    Debug.Log("Attempting to reconnect...");
                    await StartAsync(); // Call StartAsync instead of Start
                }
                await Task.Delay(5000); // Wait before retrying
            }
            catch (Exception ex)
            {
                Debug.LogError($"Reconnection failed: {ex.Message}");
            }
        }
    }

    private void SetupSubscription()
    {
        subscription = new Subscription(session.DefaultSubscription)
        {
            PublishingInterval = 1000
        };

        var items = new List<MonitoredItem>
        {
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "CurrentTime",
                StartNodeId = "i=2258"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "DriveX.fActPosition",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.IoConfig_Globals.DriveX.fActPosition"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Open_Gripper",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.AxisGroup_GVL.Open_Gripper"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "DriveY.fActPosition",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.IoConfig_Globals.DriveY.fActPosition"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "DriveZ.fActPosition",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.IoConfig_Globals.DriveZ.fActPosition"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "X_postion",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Position_to_set_in_Manuel.X"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Y_postion",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Position_to_set_in_Manuel.Y"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Z_postion",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Position_to_set_in_Manuel.Z"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "System_State",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.System_State_String"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Auto_Mode",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Auto_Mode"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Manuel_Mode",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Manuel_Mode"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Excute_Movement",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Excute_Movement"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "MoveToIDLE",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.MoveToIDLE"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Power_system",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Power_system"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "ResetGroup",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.ResetGroup"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Set_Manuel_Values",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Set_Manuel_Values"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Sen1_Box_detected",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen1_Box_detected"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Sen2_Box_picked",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen2_Box_picked"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Sen3_Ready_to_Color_detction",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen3_Ready_to_Color_detction"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Sen4_Box_placed",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen4_Box_placed"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "Sen5_",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen5_"
            },
            new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "ShutOff_system",
                StartNodeId = "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.ShutOff_system"
            }
        };

        foreach (var item in items)
        {
            item.Notification += OnMonitoredItemNotification;
        }
        subscription.AddItems(items);

        session.AddSubscription(subscription);
        subscription.Create();

        Debug.Log($"Subscribed to {items.Count} tags");
    }

    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        foreach (var value in item.DequeueValues())
        {
            mainThreadActions.Enqueue(() =>
            {
                var tag = tagValues.Find(t => t.DisplayName == item.DisplayName);
                if (tag == null)
                {
                    tag = new TagValue { DisplayName = item.DisplayName };
                    tagValues.Add(tag);
                }

                tag.Value = value.Value.ToString();
                tag.SourceTimestamp = value.SourceTimestamp.ToString();
                tag.DataType = value.WrappedValue.TypeInfo.BuiltInType;

                Debug.Log($"{item.DisplayName}: Value: {tag.Value}, SourceTimestamp: {tag.SourceTimestamp}, DataType: {tag.DataType}");

                // Notify subscribers of the updated tag
                OnTagValueUpdated?.Invoke(tag);

                // Mark the object as dirty to refresh the inspector
                EditorUtility.SetDirty(this);
            });
        }
    }

    private void Update()
    {
        while (mainThreadActions.Count > 0)
        {
            var action = mainThreadActions.Dequeue();
            action.Invoke();
        }
    }

    public async Task WriteTagValueAsync(string displayName, string value)
    {
        if (session == null || !session.Connected)
        {
            Debug.LogError("OPC UA session is not connected.");
            return;
        }

        var tag = tagValues.Find(t => t.DisplayName == displayName);
        if (tag == null)
        {
            Debug.LogError($"Tag '{displayName}' not found.");
            return;
        }

        string nodeId = GetNodeIdByDisplayName(displayName);
        if (string.IsNullOrEmpty(nodeId))
        {
            Debug.LogError($"Node ID for tag '{displayName}' is invalid.");
            return;
        }

        object convertedValue = ConvertValueToExpectedType(value, tag.DataType);
        if (convertedValue == null)
        {
            Debug.LogError($"Invalid value for tag: {displayName}. Ensure the value is in the correct format for {tag.DataType}.");
            return;
        }

        await WriteTagValueToServerAsync(nodeId, convertedValue);
    }

    private string GetNodeIdByDisplayName(string displayName)
    {
        switch (displayName)
        {
            case "DriveX.fActPosition":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.IoConfig_Globals.DriveX.fActPosition";
            case "DriveY.fActPosition":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.IoConfig_Globals.DriveY.fActPosition";
            case "DriveZ.fActPosition":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.IoConfig_Globals.DriveZ.fActPosition";
            case "Open_Gripper":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.AxisGroup_GVL.Open_Gripper";
            case "X_postion":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Position_to_set_in_Manuel.X";
            case "Y_postion":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Position_to_set_in_Manuel.Y";
            case "Z_postion":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Position_to_set_in_Manuel.Z";
            case "System_State":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.System_State_String";
            case "Auto_Mode":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Auto_Mode";
            case "Manuel_Mode":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Manuel_Mode";
            case "Excute_Movement":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Excute_Movement";
            case "MoveToIDLE":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.MoveToIDLE";
            case "Power_system":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Power_system";
            case "ResetGroup":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.ResetGroup";
            case "Set_Manuel_Values":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Set_Manuel_Values";
            case "Sen1_Box_detected":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen1_Box_detected";
            case "Sen2_Box_picked":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen2_Box_picked";
            case "Sen3_Ready_to_Color_detction":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen3_Ready_to_Color_detction";
            case "Sen4_Box_placed":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen4_Box_placed";
            case "Sen5_":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.Sen5_";
            case "ShutOff_system":
                return "ns=4;s=|var|CODESYS Control for Raspberry Pi MC SL.Application.PLC_PRG.ShutOff_system";
            default:
                return null;
        }
    }

    private object ConvertValueToExpectedType(string value, BuiltInType dataType)
    {
        value = value.Trim(); // Trim the input value

        try
        {
            switch (dataType)
            {
                case BuiltInType.Int16:
                    return short.Parse(value, CultureInfo.InvariantCulture);
                case BuiltInType.Int32:
                    return int.Parse(value, CultureInfo.InvariantCulture);
                case BuiltInType.Float:
                    return float.Parse(value, CultureInfo.InvariantCulture);
                case BuiltInType.Double:
                    return double.Parse(value, CultureInfo.InvariantCulture);
                case BuiltInType.Boolean:
                    return bool.Parse(value);
                case BuiltInType.String:
                    return value;
                default:
                    Debug.LogError($"Unsupported data type: {dataType}");
                    return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to convert value '{value}' to {dataType}: {ex.Message}");
            return null;
        }
    }

    private async Task WriteTagValueToServerAsync(string nodeId, object value)
    {
        try
        {
            Debug.Log($"Attempting to write value '{value}' to node: {nodeId}");

            var writeValue = new WriteValue
            {
                NodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var writeValues = new WriteValueCollection { writeValue };

            var requestHeader = new RequestHeader
            {
                Timestamp = DateTime.UtcNow,
                TimeoutHint = 10000,
                ReturnDiagnostics = 0
            };

            Debug.Log("Sending write request to server...");
            var writeResponse = await session.WriteAsync(
                requestHeader,
                writeValues,
                default
            );

            if (writeResponse.Results != null && writeResponse.Results.Count > 0)
            {
                if (writeResponse.Results[0] != StatusCodes.Good)
                {
                    Debug.LogError($"Failed to write value to tag: {nodeId}. StatusCode: {writeResponse.Results[0]}");
                }
                else
                {
                    Debug.Log($"Successfully wrote value to tag: {nodeId}");
                }
            }
            else
            {
                Debug.LogError("No results returned from the write operation.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error writing tag value: {ex.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        subscription?.Delete(true);
        session?.Close();
    }
}