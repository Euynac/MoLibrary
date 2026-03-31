"use strict";

let connection = null;
let hubsData = [];
let registeredListeners = new Map();
let messageCallback = null;
let connectionStatusCallback = null;
let connectionIdCallback = null;
let localization = {
    NoArguments: "No arguments",
    NotConnected: "Not connected",
    ConnectionClosedWithError: "Connection closed with error: {0}",
    ConnectionClosed: "Connection closed",
    Reconnecting: "Reconnecting...",
    ReconnectedSuccessfully: "Reconnected successfully",
    ConnectedSuccessfully: "Connected successfully",
    ConnectionFailed: "Connection failed: {0}",
    Disconnected: "Disconnected",
    RegisteredListener: "Registered listener for: {0}",
    UnregisteredListener: "Unregistered listener for: {0}",
    AllListenersCleared: "All listeners cleared"
};

function getLocalizedText(key, fallback, ...args) {
    let text = localization[key] || fallback;

    args.forEach((arg, index) => {
        text = text.replaceAll(`{${index}}`, arg ?? "");
    });

    return text;
}

// Initialize the SignalR debug functionality
const signalRDebug = {
    // Set callbacks for C# interop
    setMessageCallback: function (callback) {
        messageCallback = callback;
    },

    setConnectionStatusCallback: function (callback) {
        connectionStatusCallback = callback;
    },

    setConnectionIdCallback: function (callback) {
        connectionIdCallback = callback;
    },

    setLocalization: function (messages) {
        localization = {
            ...localization,
            ...(messages || {})
        };
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
    getHubsData: function () {
        return hubsData;
    },

    // Connect to SignalR hub
    connect: async function (hubUrl, accessToken) {
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
                        error
                            ? getLocalizedText('ConnectionClosedWithError', 'Connection closed with error: {0}', error)
                            : getLocalizedText('ConnectionClosed', 'Connection closed'),
                        'Error');
                }
            });

            connection.onreconnecting((error) => {
                if (connectionStatusCallback) {
                    connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Reconnecting');
                }
                if (messageCallback) {
                    messageCallback.invokeMethodAsync('Invoke', 'System', getLocalizedText('Reconnecting', 'Reconnecting...'), 'Info');
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
                    messageCallback.invokeMethodAsync('Invoke', 'System', getLocalizedText('ReconnectedSuccessfully', 'Reconnected successfully'), 'Success');
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
                messageCallback.invokeMethodAsync('Invoke', 'System', getLocalizedText('ConnectedSuccessfully', 'Connected successfully'), 'Success');
            }

            return { success: true };
        } catch (error) {
            if (connectionStatusCallback) {
                connectionStatusCallback.invokeMethodAsync('OnConnectionStatusChanged', 'Disconnected');
            }
            if (messageCallback) {
                messageCallback.invokeMethodAsync(
                    'Invoke',
                    'System',
                    getLocalizedText('ConnectionFailed', 'Connection failed: {0}', error.message),
                    'Error');
            }
            return { success: false, error: error.message };
        }
    },

    // Disconnect from SignalR hub
    disconnect: async function () {
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
                messageCallback.invokeMethodAsync('Invoke', 'System', getLocalizedText('Disconnected', 'Disconnected'), 'Info');
            }

            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },

    // Check if connected
    isConnected: function () {
        return connection && connection.state === signalR.HubConnectionState.Connected;
    },

    // Get connection state
    getConnectionState: function () {
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
    registerListener: function (methodName, methodDisplayName) {
        if (!connection) {
            return { success: false, error: getLocalizedText('NotConnected', 'Not connected') };
        }

        try {
            // Remove existing listener if any
            if (registeredListeners.has(methodName)) {
                connection.off(methodName);
            }

            // Register new listener
            connection.on(methodName, (...args) => {
                if (messageCallback) {
                    const argsDisplay = args.length > 0
                        ? JSON.stringify(args)
                        : getLocalizedText('NoArguments', 'No arguments');
                    messageCallback.invokeMethodAsync('Invoke', 'Received',
                        `${methodDisplayName}: ${argsDisplay}`, 'Received');
                }
            });

            registeredListeners.set(methodName, methodDisplayName);

            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System',
                    getLocalizedText('RegisteredListener', 'Registered listener for: {0}', methodDisplayName),
                    'Info');
            }

            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },

    // Unregister method listener
    unregisterListener: function (methodName, methodDisplayName) {
        if (!connection) {
            return { success: false, error: getLocalizedText('NotConnected', 'Not connected') };
        }

        try {
            connection.off(methodName);
            registeredListeners.delete(methodName);

            if (messageCallback) {
                messageCallback.invokeMethodAsync('Invoke', 'System',
                    getLocalizedText('UnregisteredListener', 'Unregistered listener for: {0}', methodDisplayName),
                    'Info');
            }

            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },

    // Invoke hub method
    invokeMethod: async function (methodName, args) {
        if (!connection) {
            return { success: false, error: getLocalizedText('NotConnected', 'Not connected') };
        }

        try {
            // No automatic type conversion is performed, keeping the type passed by C#
            // The C# side has done the correct type conversion, and the JavaScript side should be used directly.
            const convertedArgs = args.map(arg => {
                // Return parameters directly without any automatic conversion
                // Let the SignalRDebugService.ConvertParameterValue method on the C# side be responsible for type conversion
                return arg;
            });

            await connection.invoke(methodName, ...convertedArgs);

            if (messageCallback) {
                const argsDisplay = convertedArgs.length > 0
                    ? JSON.stringify(convertedArgs)
                    : getLocalizedText('NoArguments', 'No arguments');
                messageCallback.invokeMethodAsync('Invoke', 'Sent',
                    `${methodName}: ${argsDisplay}`, 'Sent');
            }

            return { success: true };
        } catch (error) {
            // Don't send error message here - let C# handle it to avoid duplicate messages
            return { success: false, error: error.message };
        }
    },

    // Send message (for quick message sending)
    sendMessage: async function (user, message) {
        return await this.invokeMethod('ReceiveTestMessage', [user, message]);
    },

    // Get registered listeners
    getRegisteredListeners: function () {
        return Array.from(registeredListeners.entries()).map(([name, displayName]) => ({
            name: name,
            displayName: displayName
        }));
    },

    // Clear all listeners
    clearAllListeners: function () {
        if (connection) {
            for (const methodName of registeredListeners.keys()) {
                connection.off(methodName);
            }
        }
        registeredListeners.clear();

        if (messageCallback) {
            messageCallback.invokeMethodAsync('Invoke', 'System', getLocalizedText('AllListenersCleared', 'All listeners cleared'), 'Info');
        }
    }
};

// Assign the signalRDebug object to window for global access
window.signalRDebug = signalRDebug;
