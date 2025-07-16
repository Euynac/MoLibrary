// SignalR Debug JavaScript API
"use strict";

let connection = null;
let hubsData = [];
let registeredListeners = new Map();
let messageCallback = null;
let connectionStatusCallback = null;
let connectionIdCallback = null;

// Initialize the SignalR debug functionality
const signalRDebug = {
    // Set callbacks for C# interop
    setMessageCallback: function(callback) {
        messageCallback = callback;
    },
    
    setConnectionStatusCallback: function(callback) {
        connectionStatusCallback = callback;
    },
    
    setConnectionIdCallback: function(callback) {
        connectionIdCallback = callback;
    },
    
    // Load available hubs from the server
    loadHubs: async function (apiUrl) {
        try {
            const response = await fetch(`${apiUrl}`);
            const apiResponse = await response.json();
            
            if (response.ok && apiResponse && !apiResponse.isFailed) {
                hubsData = apiResponse.data || [];
                return { success: true, data: hubsData };
            } else {
                const error = apiResponse?.error?.message || "Failed to load hubs";
                return { success: false, error: error };
            }
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    
    // Get currently loaded hubs data
    getHubsData: function() {
        return hubsData;
    },
    
    // Connect to SignalR hub
    connect: async function(hubUrl, accessToken) {
        try {
            // Disconnect if already connected
            if (connection) {
                await connection.stop();
            }
            
            // Create new connection
            const connectionBuilder = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, { 
                    accessTokenFactory: () => accessToken || ""
                })
                .configureLogging(signalR.LogLevel.Information)
                .withAutomaticReconnect();
            
            connection = connectionBuilder.build();
            
            // Set up connection event handlers
            connection.onclose((error) => {
                if (connectionStatusCallback) {
                    connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Disconnected');
                }
                if (connectionIdCallback) {
                    connectionIdCallback.invokeMethodAsync('SetConnectionId', '');
                }
                if (messageCallback) {
                    messageCallback.invokeMethodAsync('Invoke', 'System', 
                        error ? `Connection closed with error: ${error}` : 'Connection closed', 'Error');
                }
            });
            
            connection.onreconnecting((error) => {
                if (connectionStatusCallback) {
                    connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Reconnecting');
                }
                if (messageCallback) {
                    messageCallback.invokeMethodAsync('Invoke', 'System', 'Reconnecting...', 'Info');
                }
            });
            
            connection.onreconnected((connectionId) => {
                if (connectionStatusCallback) {
                    connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Connected');
                }
                if (connectionIdCallback) {
                    connectionIdCallback.invokeMethodAsync('SetConnectionId', connectionId || '');
                }
                if (messageCallback) {
                    messageCallback.invokeMethodAsync('Invoke', 'System', 'Reconnected successfully', 'Success');
                }
            });
            
            // Start connection
            await connection.start();
            
            console.log('SignalR connection started successfully');
            
            if (connectionStatusCallback) {
                console.log('Calling OnConnectionStatusChanged with: Connected');
                connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Connected');
            } else {
                console.log('Warning: connectionStatusCallback is null');
            }
            
            if (connectionIdCallback) {
                console.log('Calling SetConnectionId with:', connection.connectionId || '');
                connectionIdCallback.invokeMethodAsync('SetConnectionId', connection.connectionId || '');
            } else {
                console.log('Warning: connectionIdCallback is null');
            }
            
            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System', 'Connected successfully', 'Success');
            }
            
            return { success: true };
        } catch (error) {
            if (connectionStatusCallback) {
                connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Disconnected');
            }
            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System', `Connection failed: ${error.message}`, 'Error');
            }
            return { success: false, error: error.message };
        }
    },
    
    // Disconnect from SignalR hub
    disconnect: async function() {
        try {
            if (connection) {
                await connection.stop();
                connection = null;
            }
            
            // Clear all listeners
            registeredListeners.clear();
            
            if (connectionStatusCallback) {
                connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Disconnected');
            }
            if (connectionIdCallback) {
                connectionIdCallback.invokeMethodAsync('SetConnectionId', '');
            }
            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System', 'Disconnected', 'Info');
            }
            
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    
    // Check if connected
    isConnected: function() {
        return connection && connection.state === signalR.HubConnectionState.Connected;
    },
    
    // Get connection state
    getConnectionState: function() {
        if (!connection) return 'Disconnected';
        
        switch (connection.state) {
            case signalR.HubConnectionState.Connected:
                return 'Connected';
            case signalR.HubConnectionState.Connecting:
                return 'Connecting';
            case signalR.HubConnectionState.Disconnected:
                return 'Disconnected';
            case signalR.HubConnectionState.Disconnecting:
                return 'Disconnecting';
            case signalR.HubConnectionState.Reconnecting:
                return 'Reconnecting';
            default:
                return 'Unknown';
        }
    },
    
    // Register method listener
    registerListener: function(methodName, methodDisplayName) {
        if (!connection) {
            return { success: false, error: "Not connected" };
        }
        
        try {
            // Remove existing listener if any
            if (registeredListeners.has(methodName)) {
                connection.off(methodName);
            }
            
            // Register new listener
            connection.on(methodName, (...args) => {
                if (messageCallback) {
                    const argsDisplay = args.length > 0 ? JSON.stringify(args) : 'No arguments';
                    messageCallback.invokeMethodAsync('Invoke', 'Received', 
                        `${methodDisplayName}: ${argsDisplay}`, 'Received');
                }
            });
            
            registeredListeners.set(methodName, methodDisplayName);
            
            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System', 
                    `Registered listener for: ${methodDisplayName}`, 'Info');
            }
            
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    
    // Unregister method listener
    unregisterListener: function(methodName, methodDisplayName) {
        if (!connection) {
            return { success: false, error: "Not connected" };
        }
        
        try {
            connection.off(methodName);
            registeredListeners.delete(methodName);
            
            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System', 
                    `Unregistered listener for: ${methodDisplayName}`, 'Info');
            }
            
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    
    // Invoke hub method
    invokeMethod: async function(methodName, args) {
        if (!connection) {
            return { success: false, error: "Not connected" };
        }
        
        try {
            const convertedArgs = args.map(arg => {
                if (typeof arg === 'string' && arg.trim() !== '') {
                    // Try to parse as JSON first
                    try {
                        return JSON.parse(arg);
                    } catch {
                        return arg;
                    }
                }
                return arg;
            });
            
            await connection.invoke(methodName, ...convertedArgs);
            
            if (messageCallback) {
                const argsDisplay = convertedArgs.length > 0 ? JSON.stringify(convertedArgs) : 'No arguments';
                messageCallback.invokeMethodAsync('Invoke', 'Sent', 
                    `${methodName}: ${argsDisplay}`, 'Sent');
            }
            
            return { success: true };
        } catch (error) {
            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System', 
                    `Method invocation failed: ${error.message}`, 'Error');
            }
            return { success: false, error: error.message };
        }
    },
    
    // Send message (for quick message sending)
    sendMessage: async function(user, message) {
        return await this.invokeMethod('ReceiveTestMessage', [user, message]);
    },
    
    // Get registered listeners
    getRegisteredListeners: function() {
        return Array.from(registeredListeners.entries()).map(([name, displayName]) => ({
            name: name,
            displayName: displayName
        }));
    },
    
    // Clear all listeners
    clearAllListeners: function() {
        if (connection) {
            for (const methodName of registeredListeners.keys()) {
                connection.off(methodName);
            }
        }
        registeredListeners.clear();
        
                 if (messageCallback) {
             messageCallback.invokeMethodAsync('Invoke', 'System', 'All listeners cleared', 'Info');
         }
     }
};

// 将signalRDebug对象分配给window，以便全局访问
window.signalRDebug = signalRDebug;

