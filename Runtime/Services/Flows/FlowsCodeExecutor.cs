using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Puerts;
using StrixSDK.Runtime;
using Newtonsoft.Json;

namespace StrixSDK.Runtime
{
    /// <summary>
    /// Provides a secure execution environment for running user-provided JavaScript code
    /// with proper sandboxing and security boundaries.
    /// </summary>
    public class SecureJsExecutor : IDisposable
    {
        private Puerts.JsEnv jsEnv;
        private readonly int executionTimeoutMs;
        private bool isDisposed = false;

        // Patterns for potentially harmful code
        private static readonly string[] DangerousPatterns = new string[]
        {
            @"require\s*\(", // Node.js module system
            @"process\.", // Node.js process object
            @"eval\s*\(", // JavaScript eval
            @"Function\s*\(", // Function constructor
            @"localStorage", // Browser storage
            @"sessionStorage", // Browser storage
            @"document\.", // DOM access
            @"window\.", // Window object
            @"globalThis", // Global object
            @"WebSocket", // Network access
            @"fetch\s*\(", // Network request
            @"XMLHttpRequest", // Network request
            @"\$\.ajax", // jQuery ajax
            @"__dirname", // Node.js directory
            @"__filename", // Node.js filename
            @"global\.", // Node.js global object
            @"setTimeout\s*\(", // Timers (can be used for DoS)
            @"setInterval\s*\(", // Timers (can be used for DoS)
            @"importScripts", // Web Worker imports
            @"Reflect\.", // Reflection API
            @"Proxy\s*\(", // Proxy objects
            @"Object\.defineProperty", // Property redefinition
            @"constructor\.", // Constructor access
            @"prototype\.", // Prototype manipulation
            @"__proto__", // Prototype access
            @"Buffer\.", // Node.js Buffer
            @"Array\.prototype", // Array prototype manipulation
            @"Object\.prototype", // Object prototype manipulation
            @"Function\.prototype", // Function prototype manipulation
            @"fs\.", // Node.js file system
            @"child_process", // Node.js child process
            @"net\.", // Node.js net module
            @"http\.", // Node.js http module
            @"crypto\.", // Node.js crypto module
            @"os\.", // Node.js OS module
            @"path\.", // Node.js path module
            @"C#", // C# interop
            @"CS\.", // Puerts C# access
            @"System\.", // .NET System namespace
            @"UnityEngine\.", // Unity Engine namespace
            @"Debug\.", // Unity Debug
            @"Application\." // Unity Application
        };

        // Constructor
        public SecureJsExecutor(int timeoutMs = 1000)
        {
            executionTimeoutMs = timeoutMs;

            // Create a properly configured JsEnv with an explicit loader
            var loader = new Puerts.DefaultLoader();
            jsEnv = new Puerts.JsEnv(loader);

            // Register delegate types needed for JavaScript interop
            jsEnv.UsingAction<string>();
            jsEnv.UsingFunc<bool>();
            jsEnv.UsingFunc<string>();
            jsEnv.UsingFunc<string, string>();
            jsEnv.UsingFunc<object, object, object>();

            RegisterCallbacks();
        }

        private void RegisterCallbacks()
        {
            // Register console functions and other needed callbacks
            jsEnv.Eval(@"
                // Create a global reference to C# functions
                globalThis.CS_LogMessage = CS.StrixSDK.Runtime.SecureJsExecutor.LogMessage;
                globalThis.CS_LogError = CS.StrixSDK.Runtime.SecureJsExecutor.LogError;

                // Set up a safer console
                globalThis.console = {
                    log: function(msg) { CS_LogMessage(String(msg)); },
                    error: function(msg) { CS_LogError(String(msg)); }
                };
            ");
        }

        /// <summary>
        /// Safely executes JavaScript code with monetization logic
        /// </summary>
        /// <param name="jsCode">The JavaScript code to execute</param>
        /// <param name="variables">All necessary variables to execute the code</param>
        /// <returns>Result of the function execution</returns>
        public ExecutionResult ExecuteNodeCode(string jsCode, List<FlowVariableValue> variables, object prevResult)
        {
            if (isDisposed)
                throw new ObjectDisposedException("SecureJsExecutor has been disposed");

            // Validate the JS code
            if (!IsCodeSafe(jsCode))
            {
                Debug.LogError("JavaScript code contains potentially unsafe operations");
                return new ExecutionResult
                {
                    Success = false,
                    Error = "Code contains potentially unsafe operations",
                    Result = "none"
                };
            }

            // Prepare timeout tracking
            var executionFinished = false;
            var executionSuccess = false;
            var executionError = "";
            var result = new ExecutionResult { Success = false, Result = "none" };

            try
            {
                // Convert input parameters to JSON
                string contextJson = SafeJsonStringify(variables);
                string prevResultJson = SafeJsonStringify(prevResult);

                // Set global variables for use in JS
                jsEnv.Eval($"globalThis.contextData = {contextJson}");
                jsEnv.Eval($"globalThis.previousResultData = {prevResultJson}");

                // Set up a wrapper for the code that enforces return of a function
                string wrappedCode = $@"
                (function() {{
                    try {{
                        // Create secure sandbox context
                        const restrictedGlobals = {{
                            window: undefined,
                            document: undefined,
                            location: undefined,
                            navigator: undefined
                        }};

                        Object.assign(globalThis, restrictedGlobals);

                        // Execute the user code in a safe context
                        const userFunction = (function() {{
                            {jsCode}
                        }})();

                        // Validate function
                        if (typeof userFunction !== 'function') {{
                            throw new Error('Code must return a function. Check your return statement.');
                        }}

                        // Return sanitized function wrapper
                        return function() {{
                            try {{
                                const startTime = Date.now();

                                const result = userFunction(globalThis.contextData, globalThis.previousResultData);

                                if (Date.now() - startTime > {executionTimeoutMs}) {{
                                    throw new Error('Execution time limit exceeded');
                                }}

                                globalThis.lastResult = {{
                                    Success: true,
                                    Result: result,
                                    Error: """"
                                }};

                                return true;
                            }}
                            catch (error) {{
                                globalThis.lastResult = {{
                                    Success: false,
                                    Error: error.message,
                                    Result: 'none'
                                }};
                                return false;
                            }}
                        }};
                    }}
                    catch (error) {{
                        globalThis.lastResult = {{
                            Success: false,
                            Error: error.message,
                            Result: 'none'
                        }};
                        return function() {{ return false; }};
                    }}
                }})()";

                // Create a thread to monitor execution time
                var timeoutThread = new Thread(() =>
                {
                    Thread.Sleep(executionTimeoutMs + 200); // Add 200ms buffer
                    if (!executionFinished)
                    {
                        Debug.LogError("JavaScript execution timed out");
                        executionSuccess = false;
                        executionError = "Execution timed out";

                        // Force dispose the JS environment in extreme cases
                        try
                        {
                            Dispose();
                            var loader = new Puerts.DefaultLoader();
                            jsEnv = new Puerts.JsEnv(loader);
                            // Ensure we register delegates again
                            jsEnv.UsingAction<string>();
                            jsEnv.UsingFunc<bool>();
                            jsEnv.UsingFunc<string>();
                            jsEnv.UsingFunc<string, string>();
                            jsEnv.UsingFunc<object, object, object>();
                            RegisterCallbacks();
                        }
                        catch { /* Ignore errors during forced cleanup */ }
                    }
                });
                timeoutThread.Start();

                // Evaluate the code to get the monetization function and execute it
                var nodeFunction = jsEnv.Eval<Func<bool>>(wrappedCode);
                bool success = nodeFunction();

                // Get the result
                string resultJson = jsEnv.Eval<string>("JSON.stringify(globalThis.lastResult)");
                result = JsonConvert.DeserializeObject<ExecutionResult>(resultJson);

                if (result == null)
                {
                    result = new ExecutionResult
                    {
                        Success = false,
                        Error = "Failed to parse result",
                        Result = "none"
                    };
                }

                executionSuccess = success;
            }
            catch (Exception ex)
            {
                executionSuccess = false;
                executionError = ex.Message;
                Debug.LogError($"Error executing JavaScript: {ex.Message}");

                result = new ExecutionResult
                {
                    Success = false,
                    Error = ex.Message,
                    Result = "none"
                };
            }
            finally
            {
                executionFinished = true;
            }

            // If execution failed but we don't have an error message in the result
            if (!executionSuccess && string.IsNullOrEmpty(result.Error))
            {
                result.Error = executionError;
            }

            return result;
        }

        /// <summary>
        /// Safely converts an object to JSON string, handling potential serialization issues
        /// </summary>
        private string SafeJsonStringify(object obj)
        {
            if (obj == null)
                return "null";

            try
            {
                // For simple types, return directly
                if (obj is int || obj is float || obj is double || obj is bool)
                    return obj.ToString().ToLower();

                // For strings, ensure proper escaping
                if (obj is string)
                    return $"\"{EscapeJsonString((string)obj)}\"";

                // For dictionaries and complex objects, use serializer
                return JsonConvert.SerializeObject(obj, Formatting.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error serializing object to JSON: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// Properly escapes a string for JSON
        /// </summary>
        private string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f");
        }

        /// <summary>
        /// Validates JS code against known dangerous patterns
        /// </summary>
        private bool IsCodeSafe(string jsCode)
        {
            // Check for potentially dangerous code patterns
            foreach (var pattern in DangerousPatterns)
            {
                if (Regex.IsMatch(jsCode, pattern, RegexOptions.IgnoreCase))
                {
                    Debug.LogWarning($"Potentially unsafe JS code detected: {pattern}");
                    return false;
                }
            }

            return true;
        }

        // Register static callback methods for JavaScript console
        [Puerts.MonoPInvokeCallback(typeof(Action<string>))]
        public static void LogMessage(string message)
        {
            Debug.Log($"[JS]: {message}");
        }

        [Puerts.MonoPInvokeCallback(typeof(Action<string>))]
        public static void LogError(string message)
        {
            Debug.LogError($"[JS]: {message}");
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            if (!isDisposed)
            {
                jsEnv?.Dispose();
                isDisposed = true;
            }
        }

        #endregion IDisposable Implementation
    }

    /// <summary>
    /// Represents the result of monetization logic execution
    /// </summary>
    [Serializable]
    public class ExecutionResult
    {
        public bool Success;
        public object Result = "none";
        public string Error = "";
    }
}