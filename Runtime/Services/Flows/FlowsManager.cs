using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StrixSDK.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using StrixSDK.Runtime.Config;
using StrixSDK.Runtime.APIClient;
using StrixSDK.Runtime.Models;
using System.Data;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace StrixSDK.Runtime
{
    public class FlowsManager : MonoBehaviour
    {
        private static FlowsManager _instance;

        public static FlowsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FlowsManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<FlowsManager>();
                        obj.name = typeof(FlowsManager).ToString();
                    }
                }
                return _instance;
            }
        }

        // Cache of all entities without configs
        public Flow[] _flows;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private FlowExecutionPool flowExecutionPool;

        private void Start()
        {
            // Initialize pool obj for flow execution
            GameObject flowExecutionPoolObject = new GameObject("FlowExecutionPool");
            flowExecutionPool = flowExecutionPoolObject.AddComponent<FlowExecutionPool>();
            Debug.Log("FlowExecutionPool created successfully.");
        }

        public void RefreshFlows()
        {
            List<Flow> flowsList = new List<Flow>();
            var flowsDocs = Content.LoadAllFromFile("flows");

            if (flowsDocs != null)
            {
                foreach (var doc in flowsDocs)
                {
                    string json = JsonConvert.SerializeObject(doc);

                    Flow entity = JsonConvert.DeserializeObject<Flow>(json);
                    flowsList.Add(entity);
                }

                _flows = flowsList.ToArray();
                Debug.Log($"Fetched {_flows.Length} flows");
            }
            else
            {
                Debug.Log($"Could not fetch flows from persistent storage");
            }
        }

        public bool Initialize()
        {
            try
            {
                RefreshFlows();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not initialize FlowsManager. Error: {e}");
                return false;
            }
        }

        public void OnFlowEntered(string flowSid)
        {
            string segmentId = $"flow_{flowSid}";

            if (PlayerManager.Instance._playerData.Segments.Contains(segmentId))
            {
                // Loading config file
                StrixSDKConfig config = StrixSDKConfig.Instance;

                var buildType = config.branch;

                var sessionID = PlayerPrefs.GetString("Strix_SessionID", string.Empty);
                var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
                if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(clientID))
                {
                    throw new Exception("Error while executing flow. Cannot add segment to player: sessionID or clientID is null or empty");
                }

                var body = new Dictionary<string, object>()
                {
                    {"device", clientID},
                    {"secret", config.apiKey},
                    {"build", buildType},
                    {"action", "segment_add"},
                    {"payload", new Dictionary<string, object>() {
                        {"segmentID", segmentId },
                        {"flowSid", flowSid },
                    } }
                };
                _ = Client.Req(API.BackendAction, body);
                WarehouseHelperMethods.AddSegmentToPlayer(segmentId);
            }
        }

        public void ExecuteCustomFlow(string customTriggerId)
        {
            Flow flow = _flows
            .First(f => f.Nodes.Data.ContainsKey("customID") && f.Nodes.Data["customID"].ToString() == customTriggerId);

            if (flow == null)
            {
                Debug.LogError($"Flow with customID {customTriggerId} was not found.");
                return;
            }

            FlowExecution flowExecution = flowExecutionPool.GetObject();

            flowExecution.ExecuteFlow(flow, null, null);

            flowExecutionPool.ReturnObject(flowExecution);
        }

        public void ExecuteRegularFlow(string triggerId, Dictionary<string, object> contextualData)
        {
            List<Flow> flows = _flows.Where(flow => flow.Id == triggerId).ToList();

            if (flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    FlowExecution flowExecution = flowExecutionPool.GetObject();

                    flowExecution.ExecuteFlow(flow, contextualData, null);

                    flowExecutionPool.ReturnObject(flowExecution);
                }
            }
        }

        public Offer ExecuteFlow_OfferShown(Offer offer)
        {
            List<Flow> flows = _flows.Where(flow =>
            flow.Nodes.Id == "t_offerShow"
            &&
            flow.Nodes.Data.ContainsKey("offerID")
            &&
            flow.Nodes.Data["offerID"].ToString() == offer.Id)
                .ToList();

            Offer offerObject = offer;

            if (flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    FlowExecution flowExecution = flowExecutionPool.GetObject();

                    flowExecution._offer = offerObject;
                    flowExecution.ExecuteFlow(flow, null, null);
                    offerObject = flowExecution._offer;

                    flowExecutionPool.ReturnObject(flowExecution);
                }
            }
            return offerObject;
        }

        public EntityConfig ExecuteFlow_ConfigParamRetrieved(EntityConfig entityConfig)
        {
            List<Flow> flows = _flows.Where(flow =>
            flow.Nodes.Id == "t_configParamRetrieved"
            &&
            flow.Nodes.Data.ContainsKey("entityConfigID")
            &&
            flow.Nodes.Data["entityConfigID"].ToString() == entityConfig.Sid)
                .ToList();

            EntityConfig configObject = entityConfig;

            if (flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    FlowExecution flowExecution = flowExecutionPool.GetObject();

                    flowExecution._entityConfig = configObject;
                    flowExecution.ExecuteFlow(flow, null, null);
                    configObject = flowExecution._entityConfig;

                    flowExecutionPool.ReturnObject(flowExecution);
                }
            }
            return configObject;
        }

        public void ExecuteFlow_ItemAdded(string entityNodeId, int amount)
        {
            List<Flow> flows = _flows.Where(flow =>
            flow.Nodes.Id == "t_itemAdded"
            &&
            flow.Nodes.Data.ContainsKey("entityNodeID")
            &&
            flow.Nodes.Data["entityNodeID"].ToString() == entityNodeId)
                .ToList();

            var contextualData = new Dictionary<string, object>()
            {
                {"itemsAmount", amount }
            };

            if (flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    FlowExecution flowExecution = flowExecutionPool.GetObject();

                    flowExecution.ExecuteFlow(flow, contextualData, amount);

                    flowExecutionPool.ReturnObject(flowExecution);
                }
            }
        }

        public void ExecuteFlow_ItemRemoved(string entityNodeId, int amount)
        {
            List<Flow> flows = _flows.Where(flow =>
            flow.Nodes.Id == "t_itemRemoved"
            &&
            flow.Nodes.Data.ContainsKey("entityNodeID")
            &&
            flow.Nodes.Data["entityNodeID"].ToString() == entityNodeId)
                .ToList();

            var contextualData = new Dictionary<string, object>()
            {
                {"itemsAmount", amount }
            };

            if (flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    FlowExecution flowExecution = flowExecutionPool.GetObject();

                    flowExecution.ExecuteFlow(flow, contextualData, amount);

                    flowExecutionPool.ReturnObject(flowExecution);
                }
            }
        }

        public void ExecuteFlow_StatChanged(string templateId, object newValue)
        {
            List<Flow> flows = _flows.Where(flow =>
            flow.Nodes.Id == "t_statChanged"
            &&
            flow.Nodes.Data.ContainsKey("template")
            &&
            flow.Nodes.Data["template"] is NodeDataValue templateData
            &&
            templateData.Value?.ToString() == templateId)
                .ToList();

            if (flows.Count > 0)
            {
                foreach (var flow in flows)
                {
                    FlowExecution flowExecution = flowExecutionPool.GetObject();

                    flowExecution.ExecuteFlow(flow, null, newValue);

                    flowExecutionPool.ReturnObject(flowExecution);
                }
            }
        }
    }

    public class FlowExecutionPool : MonoBehaviour
    {
        private Queue<FlowExecution> pool = new Queue<FlowExecution>();
        private int initialPoolSize = 10;

        private void Awake()
        {
            // Make FlowExecution objects pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                pool.Enqueue(new FlowExecution());
            }
        }

        // Get obj from pool or create new if none
        public FlowExecution GetObject()
        {
            if (pool.Count > 0)
            {
                return pool.Dequeue(); // Get obj if exists
            }
            else
            {
                return new FlowExecution(); // If pool is empty, get a new one
            }
        }

        // Get obj back to pool
        public void ReturnObject(FlowExecution flowExecution)
        {
            flowExecution.ClearAfterPooling();
            pool.Enqueue(flowExecution);
        }
    }

    public class FlowExecution : MonoBehaviour
    {
        // Used and set locally.
        // variableValues are for regular variables like contextual or stat elements.
        // localVars are for custom saved variables.
        private List<VariableValue> variablesValues = new List<VariableValue>();

        private Dictionary<string, object> localVars = new Dictionary<string, object>();

        public class VariableValue
        {
            public string Id { get; set; }
            public object Value { get; set; }
        }

        // Contextual objects that we may process in flow and return changed.
        public EntityConfig _entityConfig = null;

        public Offer _offer = null;

        private bool flowIsStopped = false;

        private string flowSid = string.Empty;

        /// <summary>
        /// Executes flow using it's objects and some other logic.
        /// </summary>
        /// <param name="flow">Flow objects</param>
        /// <param name="contextualData">Data to append to local variables of the flow. For item added/removed this is an amount of items.</param>
        /// <param name="triggerNodeResult">The data that will be passed as a previous result to the initial node. For example, when item added/removed, that can be an amount
        /// of items added/removed, and this value will be a "return" from trigger node. Therefore, the second node in flow will be able to use it by param "Result of previous node".</param>
        public void ExecuteFlow(Flow flow, Dictionary<string, object> contextualData, object triggerNodeResult)
        {
            flowSid = flow.Id;

            FlowsManager.Instance.OnFlowEntered(flowSid);

            // Populate variables
            PopulateVariables(contextualData);
            Debug.Log("Populated variables: " + variablesValues.Count);

            // Execute flow starting from the root node
            ExecuteNode(flow.Nodes, triggerNodeResult);
        }

        public void ClearAfterPooling()
        {
            localVars = new Dictionary<string, object>();
            variablesValues = new List<VariableValue>();
            _entityConfig = null;
            _offer = null;
            flowIsStopped = false;
            flowSid = string.Empty;
        }

        private void PopulateVariables(Dictionary<string, object> contextualData)
        {
            if (PlayerManager.Instance._templates.Length > 0)
            {
                foreach (var element in PlayerManager.Instance._templates)
                {
                    var playerValue = WarehouseHelperMethods.GetPlayerElementValue(element.Id);
                    if (playerValue != null)
                    {
                        variablesValues.Add(new VariableValue { Id = element.InternalId, Value = playerValue });
                    }
                }
            }

            if (contextualData != null)
            {
                foreach (var prop in contextualData)
                {
                    var existingVariable = variablesValues.FirstOrDefault(v => v.Id == prop.Key);

                    if (existingVariable != null)
                    {
                        existingVariable.Value = prop.Value;
                    }
                    else
                    {
                        variablesValues.Add(new VariableValue { Id = prop.Key, Value = prop.Value });
                    }
                }
            }
        }

        private void UpdateTemplateVariableAfterElementChange(string templateId)
        {
            var playerValue = WarehouseHelperMethods.GetPlayerElementValueByInternalId(templateId);
            if (playerValue != null)
            {
                var existingVariable = variablesValues.FirstOrDefault(v => v.Id == templateId);

                if (existingVariable != null)
                {
                    existingVariable.Value = playerValue;
                }
                else
                {
                    variablesValues.Add(new VariableValue { Id = templateId, Value = playerValue });
                }
            }
        }

        private object GetVariableValue(string variableId)
        {
            var variable = variablesValues.Find(v => v.Id == variableId);
            return variable?.Value;
        }

        private object TryGetDataVariable(object value, bool isCustom, object previousValue, string valueType)
        {
            try
            {
                if (isCustom)
                {
                    return value;
                }
                else
                {
                    if (value.ToString() == "previousResult")
                    {
                        return previousValue;
                    }
                    else
                    {
                        if (valueType == "variable")
                        {
                            return localVars.ContainsKey(value.ToString()) ? localVars[value.ToString()] : null;
                        }
                        else
                        {
                            return GetVariableValue(value.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                throw ex;
            }
        }

        private object TryChangeDataType(object variable, string targetType)
        {
            if (string.Equals(variable.GetType().Name, targetType, StringComparison.OrdinalIgnoreCase))
            {
                return variable;
            }

            switch (targetType)
            {
                case "number":
                    return Convert.ToDouble(variable);

                case "boolean":
                case "bool":
                    return string.Equals(variable.ToString(), "true", StringComparison.OrdinalIgnoreCase);

                case "string":
                    return variable.ToString();

                default:
                    throw new InvalidOperationException($"Unsupported data type: {variable.GetType().Name}");
            }
        }

        private void SetVariableValue(string field, object value)
        {
            var existingVariable = variablesValues.Find(v => v.Id == field);
            if (existingVariable != null)
            {
                existingVariable.Value = value;
            }
            else
            {
                variablesValues.Add(new VariableValue { Id = field, Value = value });
            }
        }

        private delegate object NodeFunction(Node node, object prevResult);

        private void NodeFunctionHandler(NodeFunction func, Node node, object prevResult)
        {
            try
            {
                Debug.Log($"Started executing node {node.Id}");

                // Process node
                var result = func(node, prevResult);

                // Save result to a variable if necessary
                if (node.Data.ContainsKey("savedVariable"))
                {
                    localVars[node.Data["savedVariable"].ToString()] = result;
                }

                // Execute the next nodes
                ExecuteNextNodes(node, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing node function: {e.Message}");
            }
        }

        private void ExecuteNextNodes(Node node, object result)
        {
            switch (node.Id)
            {
                case "fc_branch":
                    // If branch, pick 0 when result is true, 1 when false
                    ExecuteNode(node.Subnodes[(bool)result ? 0 : 1], result);
                    break;

                case "fc_splitTest":
                    ExecuteNode(node.Subnodes[(int)result], result);
                    break;

                case "fc_switch":
                    ExecuteNode(node.Subnodes[(int)result], result);
                    break;

                default:
                    if (node.Id != "i_return")
                    {
                        foreach (var subnode in node.Subnodes)
                        {
                            if (!flowIsStopped)
                            {
                                ExecuteNode(subnode, result);
                            }
                        }
                    }
                    break;
            }
        }

        public static Dictionary<string, TValue> ToDictionary<TValue>(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, TValue>>(json);
            return dictionary;
        }

        private void ExecuteNode(Node node, object prevResult)
        {
            switch (node.Id)
            {
                case "ops_add":
                    NodeFunctionHandler(OpsAdd, node, prevResult);
                    break;

                case "ops_subtract":
                    NodeFunctionHandler(OpsSubtract, node, prevResult);
                    break;

                case "ops_multiply":
                    NodeFunctionHandler(OpsMultiply, node, prevResult);
                    break;

                case "ops_divide":
                    NodeFunctionHandler(OpsDivide, node, prevResult);
                    break;

                case "ops_power":
                    NodeFunctionHandler(OpsPower, node, prevResult);
                    break;

                case "ops_modulo":
                    NodeFunctionHandler(OpsModulo, node, prevResult);
                    break;

                case "ops_set":
                    NodeFunctionHandler(OpsSet, node, prevResult);
                    break;

                case "fc_branch":
                    NodeFunctionHandler(FcBranch, node, prevResult);
                    break;

                case "fc_splitTest":
                    NodeFunctionHandler(FcSplitTest, node, prevResult);
                    break;

                case "fc_switch":
                    NodeFunctionHandler(FcSwitch, node, prevResult);
                    break;

                case "fc_sequence":
                    NodeFunctionHandler(FcSequence, node, prevResult);
                    break;

                case "a_addToSegment":
                    NodeFunctionHandler(AAddToSegment, node, prevResult);
                    break;

                case "a_removeFromSegment":
                    NodeFunctionHandler(ARemoveFromSegment, node, prevResult);
                    break;

                case "a_addToStatElement":
                    NodeFunctionHandler(AAddToStatElement, node, prevResult);
                    break;

                case "a_subtractStatElement":
                    NodeFunctionHandler(ASubtractStatElement, node, prevResult);
                    break;

                case "a_setStatElement":
                    NodeFunctionHandler(ASetStatElement, node, prevResult);
                    break;

                case "a_applyChange":
                    NodeFunctionHandler(AApplyChange, node, prevResult);
                    break;

                case "a_callCustomFlow":
                    NodeFunctionHandler(ACallCustomFlow, node, prevResult);
                    break;

                case "a_showOffer":
                    NodeFunctionHandler(AShowOffer, node, prevResult);
                    break;

                case "a_sendEvent":
                    NodeFunctionHandler(ASendEvent, node, prevResult);
                    break;

                case "n_ceil":
                    NodeFunctionHandler(NCeil, node, prevResult);
                    break;

                case "n_round":
                    NodeFunctionHandler(NRound, node, prevResult);
                    break;

                case "n_getRandomNumber":
                    NodeFunctionHandler(NGetRandomNumber, node, prevResult);
                    break;

                case "n_floor":
                    NodeFunctionHandler(NFloor, node, prevResult);
                    break;

                case "n_clamp":
                    NodeFunctionHandler(NClamp, node, prevResult);
                    break;

                case "t_offerShow":
                    NodeFunctionHandler(TOfferShow, node, prevResult);
                    break;

                case "t_custom":
                    NodeFunctionHandler(TCustom, node, prevResult);
                    break;

                case "t_configParamRetrieved":
                    NodeFunctionHandler(TConfigParamRetrieved, node, prevResult);
                    break;

                case "t_itemAdded":
                    NodeFunctionHandler(TItemAdded, node, prevResult);
                    break;

                case "t_itemRemoved":
                    NodeFunctionHandler(TItemRemoved, node, prevResult);
                    break;

                case "t_offerBought":
                    NodeFunctionHandler(TOfferBought, node, prevResult);
                    break;

                case "t_segmentExit":
                    NodeFunctionHandler(TSegmentExit, node, prevResult);
                    break;

                case "t_segmentJoin":
                    NodeFunctionHandler(TSegmentJoin, node, prevResult);
                    break;

                case "t_statChanged":
                    NodeFunctionHandler(TStatChanged, node, prevResult);
                    break;

                case "i_return":
                    NodeFunctionHandler(IReturn, node, prevResult);
                    break;
            }

            // i_return
            object IReturn(Node node, object prevResult)
            {
                flowIsStopped = true;
                var result = true;
                return result;
            }

            // t_custom
            object TCustom(Node node, object prevResult)
            {
                var result = true;
                return result;
            }

            // t_configParamRetrieved
            object TConfigParamRetrieved(Node node, object prevResult)
            {
                Debug.Log($"Trigger config \"{_entityConfig.Id}\"");
                var result = true;
                return result;
            }

            // t_itemAdded
            object TItemAdded(Node node, object prevResult)
            {
                var result = prevResult;
                return result;
            }

            // t_itemRemoved
            object TItemRemoved(Node node, object prevResult)
            {
                var result = prevResult;
                return result;
            }

            // t_offerBought
            object TOfferBought(Node node, object prevResult)
            {
                Offer offer = OffersManager.Instance._offers.First(o => o.InternalId == (string)node.Data["offerID"]);
                Debug.Log($"Trigger offer: {offer.Name}");

                variablesValues.Add(new VariableValue { Id = "offerIcon", Value = offer.Icon });
                variablesValues.Add(new VariableValue { Id = "offerPrice", Value = offer.Price.Value });
                variablesValues.Add(new VariableValue { Id = "offerDiscount", Value = offer.Pricing.Discount });

                var result = true;

                return result;
            }

            // t_segmentExit
            object TSegmentExit(Node node, object prevResult)
            {
                var result = prevResult;
                return result;
            }

            // t_segmentJoin
            object TSegmentJoin(Node node, object prevResult)
            {
                var result = prevResult;
                return result;
            }

            // t_statChanged
            object TStatChanged(Node node, object prevResult)
            {
                var result = prevResult;
                return result;
            }

            // t_offerShow
            object TOfferShow(Node node, object prevResult)
            {
                Offer offer = OffersManager.Instance._offers.First(o => o.InternalId == (string)node.Data["offerID"]);
                Debug.Log($"Trigger offer: {offer.Name}");

                variablesValues.Add(new VariableValue { Id = "offerIcon", Value = offer.Icon });
                variablesValues.Add(new VariableValue { Id = "offerPrice", Value = offer.Price.Value });
                variablesValues.Add(new VariableValue { Id = "offerDiscount", Value = offer.Pricing.Discount });

                var result = true;

                return result;
            }

            // n_ceil
            object NCeil(Node node, object prevResult)
            {
                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }

                Debug.Log($"Ceiling value {value1.Value.ToString()} ({value1.Type})");

                var var1 = (double)TryGetDataVariable(value1.Value, value1.IsCustom, prevResult, value1.Type);

                var result = Math.Ceiling(var1);

                return result;
            }

            // n_round
            object NRound(Node node, object prevResult)
            {
                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }

                Debug.Log($"Rounding value {node.Data["value"].ToString()} ({node.Data["valueType"]})");

                var var1 = (double)TryGetDataVariable(value1.Value, value1.IsCustom, prevResult, value1.Type);

                var result = Math.Round(var1);

                return result;
            }

            // n_getRandomNumber
            object NGetRandomNumber(Node node, object prevResult)
            {
                NodeDataValue valueMin = null;
                if (node.Data.ContainsKey("valueMin") && node.Data["valueMin"] is JObject valueMinObject)
                {
                    valueMin = valueMinObject.ToObject<NodeDataValue>();
                }
                NodeDataValue valueMax = null;
                if (node.Data.ContainsKey("valueMax") && node.Data["valueMax"] is JObject valueMaxObject)
                {
                    valueMax = valueMaxObject.ToObject<NodeDataValue>();
                }
                Debug.Log($"Getting random value between {valueMin.Value} ({valueMin.Type}) and {valueMax.Value} ({valueMax.Type})");

                var varMin = (float)TryGetDataVariable(valueMin.Value, valueMin.IsCustom, prevResult, valueMin.Type);
                var varMax = (float)TryGetDataVariable(valueMax.Value, valueMax.IsCustom, prevResult, valueMax.Type);

                var result = UnityEngine.Random.Range(varMin, varMax);

                return result;
            }

            // n_floor
            object NFloor(Node node, object prevResult)
            {
                NodeDataValue value = null;
                if (node.Data.ContainsKey("value") && node.Data["value"] is JObject valueObject)
                {
                    value = valueObject.ToObject<NodeDataValue>();
                }

                Debug.Log($"Flooring value {value.Value} ({value.Type})");

                var var1 = (float)TryGetDataVariable(value.Value, value.IsCustom, prevResult, value.Type);

                var result = Mathf.FloorToInt(var1);

                return result;
            }

            // n_clamp
            object NClamp(Node node, object prevResult)
            {
                NodeDataValue value = null;
                if (node.Data.ContainsKey("value") && node.Data["value"] is JObject valueObject)
                {
                    value = valueObject.ToObject<NodeDataValue>();
                }
                NodeDataValue valueMin = null;
                if (node.Data.ContainsKey("valueMin") && node.Data["valueMin"] is JObject valueMinObject)
                {
                    valueMin = valueMinObject.ToObject<NodeDataValue>();
                }
                NodeDataValue valueMax = null;
                if (node.Data.ContainsKey("valueMax") && node.Data["valueMax"] is JObject valueMaxObject)
                {
                    valueMax = valueMaxObject.ToObject<NodeDataValue>();
                }

                Debug.Log($"Clamping value {value.Value} ({value.Type}) between {valueMin.Value} ({valueMin.Type}) and {valueMax.Value} ({valueMax.Type})");

                var var1 = (float)TryGetDataVariable(value.Value, value.IsCustom, prevResult, value.Type);
                var varMin = (float)TryGetDataVariable(valueMin.Value, valueMin.IsCustom, prevResult, valueMin.Type);
                var varMax = (float)TryGetDataVariable(valueMax.Value, valueMax.IsCustom, prevResult, valueMax.Type);

                var result = Mathf.Clamp(var1, varMin, varMax);

                return result;
            }

            // a_sendEvent
            object ASendEvent(Node node, object prevResult)
            {
                List<NodeDataEventCustomData> customDataObj = null;
                if (node.Data.ContainsKey("customData") && node.Data["customData"] is JArray customDataArray)
                {
                    customDataObj = customDataArray.ToObject<List<NodeDataEventCustomData>>();
                }

                Dictionary<string, object> customData = new Dictionary<string, object>();
                foreach (var item in customDataObj)
                {
                    object var1 = TryGetDataVariable(item.Value.Value, item.Value.IsCustom, prevResult, item.Value.Type);
                    var1 = TryChangeDataType(var1, item.Value.Type);
                    customData.Add(item.Field, var1);
                }
                _ = Analytics.SendCustomEvent((string)node.Data["eventID"], customData);

                Debug.Log($"Event with ID \"{node.Data["eventID"]}\" should be sent with {customDataObj.Count} additional custom fields.");
                var result = true;
                return result;
            }

            // fc_sequence
            object FcSequence(Node node, object prevResult)
            {
                var result = true;
                return result;
            }

            // a_setStatElement
            object ASetStatElement(Node node, object prevResult)
            {
                NodeDataValue value = null;
                if (node.Data.ContainsKey("value") && node.Data["value"] is JObject valueObject)
                {
                    value = valueObject.ToObject<NodeDataValue>();
                }
                var var1 = TryGetDataVariable(value.Value, value.IsCustom, prevResult, value.Type);

                NodeDataValue templateInternalId = null;
                if (node.Data.ContainsKey("template") && node.Data["template"] is JObject templateInternalIdObject)
                {
                    templateInternalId = templateInternalIdObject.ToObject<NodeDataValue>();
                }
                ElementTemplate template = WarehouseHelperMethods.GetTemplateByInternalId((string)templateInternalId.Value);
                WarehouseHelperMethods.SetPlayerElementValue(template.Id, var1);

                SetVariableValue(template.Id, var1);
                UpdateTemplateVariableAfterElementChange((string)templateInternalId.Value);

                var result = var1;
                return result;
            }

            // a_showOffer
            object AShowOffer(Node node, object prevResult)
            {
                var offers = new List<Offer>();

                var o = OffersHelperMethods.GetOfferByInternalId((string)node.Data["offerID"], true);
                offers.Add(o);

                OffersHelperMethods.InvokeTriggeredOffers(offers);

                Debug.Log($"Offer with ID \"{node.Data["offerID"]}\" should be returned in the event \"OnOffersTriggered\" by now.");
                var result = true;
                return result;
            }

            // a_callCustomFlow
            object ACallCustomFlow(Node node, object prevResult)
            {
                var result = node.Data["customTriggerID"];
                if (!string.IsNullOrEmpty((string)result))
                {
                    FlowsManager.Instance.ExecuteCustomFlow((string)result);
                }
                else
                {
                    Debug.Log($"Could not call custom flow. ID to call was null or empty.");
                    result = false;
                }

                return result;
            }

            // a_subtractStatElement
            object ASubtractStatElement(Node node, object prevResult)
            {
                NodeDataValue value = null;
                if (node.Data.ContainsKey("value") && node.Data["value"] is JObject valueObject)
                {
                    value = valueObject.ToObject<NodeDataValue>();
                }
                var var1 = TryGetDataVariable(value.Value, value.IsCustom, prevResult, value.Type);

                NodeDataValue templateInternalId = null;
                if (node.Data.ContainsKey("template") && node.Data["template"] is JObject templateInternalIdObject)
                {
                    templateInternalId = templateInternalIdObject.ToObject<NodeDataValue>();
                }

                ElementTemplate template = WarehouseHelperMethods.GetTemplateByInternalId((string)templateInternalId.Value);
                WarehouseHelperMethods.SubtractPlayerElementValue(template.Id, var1);

                SetVariableValue(template.Id, var1);
                UpdateTemplateVariableAfterElementChange((string)templateInternalId.Value);

                var result = var1;
                return result;
            }

            // a_addToStatElement
            object AAddToStatElement(Node node, object prevResult)
            {
                NodeDataValue value = null;
                if (node.Data.ContainsKey("value") && node.Data["value"] is JObject valueObject)
                {
                    value = valueObject.ToObject<NodeDataValue>();
                }
                var var1 = TryGetDataVariable(value.Value, value.IsCustom, prevResult, value.Type);

                NodeDataValue templateInternalId = null;
                if (node.Data.ContainsKey("template") && node.Data["template"] is JObject templateInternalIdObject)
                {
                    templateInternalId = templateInternalIdObject.ToObject<NodeDataValue>();
                }

                ElementTemplate template = WarehouseHelperMethods.GetTemplateByInternalId((string)templateInternalId.Value);
                WarehouseHelperMethods.AddPlayerElementValue(template.Id, var1);

                SetVariableValue(template.Id, var1);
                UpdateTemplateVariableAfterElementChange((string)templateInternalId.Value);

                var result = var1;
                return result;
            }

            // a_removeFromSegment
            object ARemoveFromSegment(Node node, object prevResult)
            {
                // Loading config file
                StrixSDKConfig config = StrixSDKConfig.Instance;

                var buildType = config.branch;

                var sessionID = PlayerPrefs.GetString("Strix_SessionID", string.Empty);
                var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
                if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(clientID))
                {
                    throw new Exception("Error while executing flow. Cannot remove player from segment: sessionID or clientID is null or empty");
                }

                var body = new Dictionary<string, object>()
                {
                    {"device", clientID},
                    {"secret", config.apiKey},
                    {"build", buildType},
                    {"action", "segment_remove"},
                    {"payload", new Dictionary<string, object>() {
                        {"segmentID", node.Data["segmentID"] },
                        {"flowSid", flowSid },
                        {"nodeSid", node.Sid },
                    } }
                };
                _ = Client.Req(API.BackendAction, body);
                WarehouseHelperMethods.RemoveSegmentFromPlayer((string)node.Data["segmentID"]);

                Debug.Log($"Removing segment \"{node.Data["segmentID"]}\" from player");
                var result = true;
                return result;
            }

            // a_addToSegment
            object AAddToSegment(Node node, object prevResult)
            {
                // Loading config file
                StrixSDKConfig config = StrixSDKConfig.Instance;

                var buildType = config.branch;

                var sessionID = PlayerPrefs.GetString("Strix_SessionID", string.Empty);
                var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
                if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(clientID))
                {
                    throw new Exception("Error while executing flow. Cannot add segment to player: sessionID or clientID is null or empty");
                }

                var body = new Dictionary<string, object>()
                {
                    {"device", clientID},
                    {"secret", config.apiKey},
                    {"build", buildType},
                    {"action", "segment_add"},
                    {"payload", new Dictionary<string, object>() {
                        {"segmentID", node.Data["segmentID"] },
                        {"flowSid", flowSid },
                        {"nodeSid", node.Sid },
                    } }
                };
                _ = Client.Req(API.BackendAction, body);
                WarehouseHelperMethods.AddSegmentToPlayer((string)node.Data["segmentID"]);

                Debug.Log($"Adding segment \"{node.Data["segmentID"]}\" to player");
                var result = true;
                return result;
            }

            // a_applyChange
            object AApplyChange(Node node, object prevResult)
            {
                NodeDataValue fieldToSet = null;
                if (node.Data.ContainsKey("fieldToSet") && node.Data["fieldToSet"] is JObject fieldToSetObject)
                {
                    fieldToSet = fieldToSetObject.ToObject<NodeDataValue>();
                }

                var result = node.Data["value"];

                switch (fieldToSet.Type)
                {
                    case "offerIcon":
                        var fileName = Path.GetFileName((string)result);

                        // Check if the file exists in the cache
                        if (Content.DoesMediaExist(fileName))
                        {
                            // If the file exists, set it's name now instead of the old one
                            _offer.Icon = fileName;
                        }
                        else
                        {
                            Debug.LogError($"Error at AApplyChange: tried to set '{fieldToSet.Type}', but the value is '{result.ToString()}'");
                        }
                        break;

                    case "offerPrice":
                        _offer.Price.Value = (float)result;
                        break;

                    case "offerDiscount":
                        _offer.Pricing.Discount = (int)result;
                        break;

                    case "entityConfigValue":
                        var changed = false;
                        foreach (var value in _entityConfig.RawValues)
                        {
                            if (value.Sid == (string)fieldToSet.Value)
                            {
                                var firstMatchingSegment = EntityHelperMethods.PickAppropriateSegmentedValue(value);
                                var segmentValue = value.Segments.FirstOrDefault(s => s.SegmentID == firstMatchingSegment.SegmentID);
                                if (segmentValue != null)
                                {
                                    segmentValue.Value = (string)result;
                                    changed = true;
                                }
                                break;
                            }
                            if (value.Values != null)
                            {
                                foreach (var subValue in value.Values)
                                {
                                    if (subValue.Sid == (string)fieldToSet.Value)
                                    {
                                        var firstMatchingSegment = EntityHelperMethods.PickAppropriateSegmentedValue(subValue);
                                        var segmentValue = subValue.Segments.FirstOrDefault(s => s.SegmentID == firstMatchingSegment.SegmentID);
                                        if (segmentValue != null)
                                        {
                                            segmentValue.Value = (string)result;
                                            changed = true;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        if (!changed)
                        {
                            Debug.LogError($"Error while applying change in flow. Tried to change entity config value, but couldn't find the value to change. Config remains unchanged.");
                        }
                        break;

                    default:
                        Debug.LogError($"Error while applying change in flow. Met unknown '{fieldToSet.Type}' type.");
                        break;
                }

                return (result);
            }

            // fc_switch
            object FcSwitch(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue field = null;
                if (node.Data.ContainsKey("field") && node.Data["field"] is JObject fieldObject)
                {
                    field = fieldObject.ToObject<NodeDataValue>();
                }

                var var1 = TryGetDataVariable(
                field.Value,
                    field.IsCustom,
                prevResult,
                    field.Type
                );

                List<NodeDataCases> cases = null;
                if (node.Data.ContainsKey("cases") && node.Data["cases"] is JObject casesObject)
                {
                    cases = casesObject.ToObject<List<NodeDataCases>>();
                }

                var result = cases.FindIndex(c => c.IsDefault == true);

                for (int i = 0; i < cases.Count; i++)
                {
                    if (cases[i].IsDefault != true)
                    {
                        if (var1.ToString() == cases[i].Type)
                        {
                            Debug.Log($"Switching on field {field.Value} with value \"{var1}\". Case \"{cases[i].Type}\": true");
                            result = i;
                            return result;
                        }
                        else
                        {
                            Debug.Log($"Switching on field {field.Value} with value \"{var1}\". Case \"{cases[i].Type}\": false");
                        }
                    }
                }

                return ((object)result);
            }

            // fc_branch
            object FcBranch(Node node, object prevResult)
            {
                List<string> logging = new List<string>();
                bool result = true;

                List<NodeDataConditions> conditions = null;
                if (node.Data.ContainsKey("conditions") && node.Data["conditions"] is JArray conditionsObject)
                {
                    conditions = conditionsObject.ToObject<List<NodeDataConditions>>();
                }

                foreach (var cond in conditions)
                {
                    string dataType = "";
                    switch (cond.Operator)
                    {
                        case "=":
                        case "!=":
                        case "includes":
                            dataType = "string";
                            break;

                        case ">":
                        case "<":
                        case ">=":
                        case "<=":
                            dataType = "number";
                            break;
                    }

                    var var1 = TryGetDataVariable(
                        cond.Value1.Value,
                        cond.Value1.IsCustom,
                        prevResult,
                        cond.Value1.Type
                    );
                    var1 = TryChangeDataType(var1, dataType);

                    var var2 = TryGetDataVariable(
                        cond.Value2.Value,
                        cond.Value2.IsCustom,
                        prevResult,
                        cond.Value2.Type
                    );
                    var2 = TryChangeDataType(var2, dataType);

                    // Set the result to false if any condition fails
                    switch (cond.Operator)
                    {
                        case "=":
                            if (!var1.Equals(var2))
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {var1.Equals(var2)}");
                            break;

                        case "!=":
                            if (var1.Equals(var2))
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {var1.Equals(var2)}");
                            break;

                        case "includes":
                            if (!((string)var1).Contains((string)var2))
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {((string)var1).Contains((string)var2)}");
                            break;

                        case ">":
                            if ((double)var1 < (double)var2)
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {(double)var1 > (double)var2}");
                            break;

                        case "<":
                            if ((double)var1 > (double)var2)
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {(double)var1 < (double)var2}");
                            break;

                        case ">=":
                            if ((double)var1 <= (double)var2)
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {(double)var1 >= (double)var2}");
                            break;

                        case "<=":
                            if ((double)var1 >= (double)var2)
                            {
                                result = false;
                            }
                            Debug.Log($"Condition: {var1} ({var1.GetType()}) {cond.Operator} {var2} ({var2.GetType()}). Result: {(double)var1 <= (double)var2}");
                            break;
                    }
                }

                return (object)result;
            }

            // fc_splitTest
            object FcSplitTest(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                List<double> cumulativeProbabilities = new List<double>();
                double cumulativeSum = 0;

                List<NodeDataSplits> splits = null;
                if (node.Data.ContainsKey("splits") && node.Data["splits"] is JObject splitsObject)
                {
                    splits = splitsObject.ToObject<List<NodeDataSplits>>();
                }

                // Make an array of probabilities
                foreach (var split in splits)
                {
                    cumulativeSum += split.Share;
                    cumulativeProbabilities.Add(cumulativeSum);
                }

                var numbers = cumulativeProbabilities.Select((p, i) => i).ToList();

                var result = numbers[numbers.Count - 1];

                for (int i = 0; i < splits.Count; i++)
                {
                    string segmentId = $"flow_{flowSid}_splitTest_{node.Sid}_{splits[i].Sid}";
                    if (PlayerManager.Instance._playerData.Segments.Contains(segmentId))
                    {
                        return result;
                    }
                }

                var randomValue = UnityEngine.Random.Range(0, 100); // Get random value
                for (int i = 0; i < cumulativeProbabilities.Count; i++)
                {
                    if (randomValue < cumulativeProbabilities[i])
                    {
                        // Loading config file
                        StrixSDKConfig config = StrixSDKConfig.Instance;

                        var buildType = config.branch;

                        var sessionID = PlayerPrefs.GetString("Strix_SessionID", string.Empty);
                        var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
                        if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(clientID))
                        {
                            throw new Exception("Error while executing flow. Cannot add segment to player: sessionID or clientID is null or empty");
                        }

                        string segmentId = $"flow_{flowSid}_splitTest_{node.Sid}_{splits[i].Sid}";
                        var body = new Dictionary<string, object>()
                        {
                            {"device", clientID},
                            {"secret", config.apiKey},
                            {"build", buildType},
                            {"action", "segment_add"},
                            {"payload", new Dictionary<string, object>() {
                                {"segmentID", segmentId },
                                {"flowSid", flowSid },
                                {"nodeSid", node.Sid },
                            } }
                        };
                        _ = Client.Req(API.BackendAction, body);
                        WarehouseHelperMethods.AddSegmentToPlayer(segmentId);

                        Debug.Log($"Choosing random audience path. Path {numbers[i]} was chosen");
                        result = numbers[i]; // Pick appropriate path

                        return result;
                    }
                }

                return ((object)result);
            }

            // ops_add
            object OpsAdd(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value2 = null;
                if (node.Data.ContainsKey("value2") && node.Data["value2"] is JObject value2Object)
                {
                    value2 = value2Object.ToObject<NodeDataValue>();
                }

                Debug.Log(
                    $"Summing {value1.Value} ({value1.Type}) and {value2.Value} ({value2.Type})"
                );

                var var1 = TryGetDataVariable(
                    value1.Value,
                    value1.IsCustom,
                    prevResult,
                    value1.Type
                );

                var var2 = TryGetDataVariable(
                    value2.Value,
                    value2.IsCustom,
                    prevResult,
                    value2.Type
                );

                if (var1.GetType() != typeof(double))
                {
                    var1 = TryChangeDataType(var1, "number");
                }

                if (var2.GetType() != typeof(double))
                {
                    var2 = TryChangeDataType(var2, "number");
                }

                double result = Convert.ToDouble(var1) + Convert.ToDouble(var2);

                return (object)result;
            }

            // ops_subtract
            object OpsSubtract(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value2 = null;
                if (node.Data.ContainsKey("value2") && node.Data["value2"] is JObject value2Object)
                {
                    value2 = value2Object.ToObject<NodeDataValue>();
                }
                Debug.Log(
                    $"Subtracting {value1.Value} ({value1.Type}) and {value2.Value} ({value2.Type})"
                );

                var var1 = TryGetDataVariable(
                    value1.Value,
                    value1.IsCustom,
                    prevResult,
                    value1.Type
                );

                var var2 = TryGetDataVariable(
                    value2.Value,
                    value2.IsCustom,
                    prevResult,
                    value2.Type
                );

                if (var1.GetType() != typeof(double))
                {
                    var1 = TryChangeDataType(var1, "number");
                }

                if (var2.GetType() != typeof(double))
                {
                    var2 = TryChangeDataType(var2, "number");
                }

                double result = Convert.ToDouble(var1) - Convert.ToDouble(var2);

                return (object)result;
            }

            // ops_multiply
            object OpsMultiply(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value2 = null;
                if (node.Data.ContainsKey("value2") && node.Data["value2"] is JObject value2Object)
                {
                    value2 = value2Object.ToObject<NodeDataValue>();
                }
                Debug.Log(
                    $"Multiplying {value1.Value} ({value1.Type}) and {value2.Value} ({value2.Type})"
                );

                var var1 = TryGetDataVariable(
                    value1.Value,
                    value1.IsCustom,
                    prevResult,
                    value1.Type
                );

                var var2 = TryGetDataVariable(
                    value2.Value,
                    value2.IsCustom,
                    prevResult,
                    value2.Type
                );

                if (var1.GetType() != typeof(double))
                {
                    var1 = TryChangeDataType(var1, "number");
                }

                if (var2.GetType() != typeof(double))
                {
                    var2 = TryChangeDataType(var2, "number");
                }

                double result = Convert.ToDouble(var1) * Convert.ToDouble(var2);

                return (object)result;
            }

            // ops_divide
            object OpsDivide(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value2 = null;
                if (node.Data.ContainsKey("value2") && node.Data["value2"] is JObject value2Object)
                {
                    value2 = value2Object.ToObject<NodeDataValue>();
                }

                Debug.Log(
                    $"Dividing {value1.Value} ({value1.Type}) and {value2.Value} ({value2.Type})"
                );

                var var1 = TryGetDataVariable(
                    value1.Value,
                    value1.IsCustom,
                    prevResult,
                    value1.Type
                );

                var var2 = TryGetDataVariable(
                    value2.Value,
                    value2.IsCustom,
                    prevResult,
                    value2.Type
                );

                if (var1.GetType() != typeof(double))
                {
                    var1 = TryChangeDataType(var1, "number");
                }

                if (var2.GetType() != typeof(double))
                {
                    var2 = TryChangeDataType(var2, "number");
                }

                double result = Convert.ToDouble(var1) / Convert.ToDouble(var2);

                return (object)result;
            }

            // ops_power
            object OpsPower(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value2 = null;
                if (node.Data.ContainsKey("value2") && node.Data["value2"] is JObject value2Object)
                {
                    value2 = value2Object.ToObject<NodeDataValue>();
                }

                Debug.Log(
                    $"Finding power of {value1.Value} ({value1.Type}) and {value2.Value} ({value2.Type})"
                );

                var var1 = TryGetDataVariable(
                    value1.Value,
                    value1.IsCustom,
                    prevResult,
                    value1.Type
                );

                var var2 = TryGetDataVariable(
                    value2.Value,
                    value2.IsCustom,
                    prevResult,
                    value2.Type
                );

                if (var1.GetType() != typeof(double))
                {
                    var1 = TryChangeDataType(var1, "number");
                }

                if (var2.GetType() != typeof(double))
                {
                    var2 = TryChangeDataType(var2, "number");
                }

                double result = Math.Pow(Convert.ToDouble(var1), Convert.ToDouble(var2));

                return (object)result;
            }

            // ops_modulo
            object OpsModulo(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue value1 = null;
                if (node.Data.ContainsKey("value1") && node.Data["value1"] is JObject value1Object)
                {
                    value1 = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value2 = null;
                if (node.Data.ContainsKey("value2") && node.Data["value2"] is JObject value2Object)
                {
                    value2 = value2Object.ToObject<NodeDataValue>();
                }

                Debug.Log(
                    $"Finding modulo between {value1.Value} ({value1.Type}) and {value2.Value} ({value2.Type})"
                );

                var var1 = TryGetDataVariable(
                    value1.Value,
                    value1.IsCustom,
                    prevResult,
                    value1.Type
                );

                var var2 = TryGetDataVariable(
                    value2.Value,
                    value2.IsCustom,
                    prevResult,
                    value2.Type
                );

                if (var1.GetType() != typeof(double))
                {
                    var1 = TryChangeDataType(var1, "number");
                }

                if (var2.GetType() != typeof(double))
                {
                    var2 = TryChangeDataType(var2, "number");
                }

                double result = Convert.ToDouble(var1) % Convert.ToDouble(var2);

                return (object)result;
            }

            // ops_set
            object OpsSet(Node node, object prevResult)
            {
                List<string> logging = new List<string>();

                NodeDataValue fieldToSet = null;
                if (node.Data.ContainsKey("fieldToSet") && node.Data["fieldToSet"] is JObject value1Object)
                {
                    fieldToSet = value1Object.ToObject<NodeDataValue>();
                }
                NodeDataValue value = null;
                if (node.Data.ContainsKey("value") && node.Data["value"] is JObject valueObject)
                {
                    value = valueObject.ToObject<NodeDataValue>();
                }

                Debug.Log(
                    $"Setting field {fieldToSet.Value} ({fieldToSet.Type}) to {value.Value} ({value.Type})"
                );

                var var1 = TryGetDataVariable(
                    value.Value,
                    value.IsCustom,
                    prevResult,
                    value.Type
                );
                var1 = TryChangeDataType(var1, fieldToSet.Type);

                SetVariableValue((string)fieldToSet.Value, var1);

                double result = Convert.ToDouble(var1);

                return (object)result;
            }
        }
    }
}