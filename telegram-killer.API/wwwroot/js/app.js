
    const schema = {
  "asyncapi": "2.6.0",
  "info": {
    "title": "ChatHub API",
    "version": "2.0.0",
    "description": "Real-time chat API powered by ASP.NET Core SignalR over WebSockets.\n\n## Authentication\nAll connections must carry a valid JWT bearer token. Connections that\narrive without a resolved UserIdentifier are immediately aborted by the\nserver (Context.Abort()).\n\n## Connection Lifecycle\n- OnConnectedAsync: Server validates UserIdentifier; missing aborts connection.\n- Active: Client may invoke JoinChat or SendMessage; server pushes ReceiveMessage.\n- OnDisconnectedAsync: Server logs normal or error-caused disconnection.\n"
  },
  "servers": {
    "development": {
      "url": "ws://localhost:8080/hub/chat",
      "protocol": "ws",
      "description": "Local development ChatHub SignalR endpoint (WS)",
      "security": [
        {
          "bearerAuth": []
        }
      ]
    }
  },
  "channels": {
    "chat/joinChat": {
      "description": "Client invokes this channel to join a chat group. Maps directly to\n`JoinChat(string chatId)` hub method.\n",
      "publish": {
        "operationId": "joinChat",
        "summary": "Join a chat group",
        "tags": [
          {
            "name": "group"
          }
        ],
        "message": {
          "name": "JoinChat",
          "title": "Join Chat Request",
          "summary": "Payload the client sends to join a chat group",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId"
            ],
            "additionalProperties": false,
            "properties": {
              "chatId": {
                "type": "string",
                "minLength": 1,
                "description": "The chat group identifier to join (GUID).",
                "example": "d4f1a2b3-1000-0000-0000-000000000001",
                "x-parser-schema-id": "<anonymous-schema-1>"
              }
            },
            "x-parser-schema-id": "JoinChatPayload"
          },
          "examples": [
            {
              "name": "JoinGeneralChat",
              "summary": "Join a chat group",
              "payload": {
                "chatId": "d4f1a2b3-1000-0000-0000-000000000001"
              }
            }
          ]
        }
      }
    },
    "chat/sendMessage": {
      "description": "Client invokes this channel to dispatch a message to a chat group.\nMaps to `SendMessage(string chatId, string content)` hub method.\nThe caller's UserIdentifier is resolved server-side.\n",
      "publish": {
        "operationId": "sendMessage",
        "summary": "Send a message to a chat group",
        "tags": [
          {
            "name": "messaging"
          }
        ],
        "message": {
          "name": "SendMessage",
          "title": "Send Message Request",
          "summary": "Payload the client sends when invoking the SendMessage hub method",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId",
              "content"
            ],
            "additionalProperties": false,
            "properties": {
              "chatId": {
                "type": "string",
                "minLength": 1,
                "description": "The chat group identifier (GUID) to send the message to.",
                "example": "d4f1a2b3-1000-0000-0000-000000000001",
                "x-parser-schema-id": "<anonymous-schema-2>"
              },
              "content": {
                "type": "string",
                "minLength": 1,
                "description": "The plain-text content of the message.",
                "example": "Hello everyone!",
                "x-parser-schema-id": "<anonymous-schema-3>"
              }
            },
            "x-parser-schema-id": "SendMessagePayload"
          },
          "examples": [
            {
              "name": "TextMessage",
              "summary": "Send a text message to a chat group",
              "payload": {
                "chatId": "d4f1a2b3-1000-0000-0000-000000000001",
                "content": "Hello everyone!"
              }
            }
          ]
        }
      }
    },
    "chat/receiveMessage": {
      "description": "Server pushes this event to all members of a chat group when a message\nis sent via `SendMessage`.\n",
      "subscribe": {
        "operationId": "receiveMessage",
        "summary": "Receive a message from a chat group",
        "tags": [
          {
            "name": "messaging"
          }
        ],
        "message": {
          "name": "ReceiveMessage",
          "title": "Receive Message Event",
          "summary": "Payload the server pushes to the chat group",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "id",
              "chatId",
              "senderId",
              "content",
              "sentAt"
            ],
            "additionalProperties": false,
            "properties": {
              "id": {
                "type": "string",
                "description": "Unique identifier of the message.",
                "example": "f1a2b3c4-0000-0000-0000-000000000001",
                "x-parser-schema-id": "<anonymous-schema-4>"
              },
              "chatId": {
                "type": "string",
                "description": "The chat group identifier.",
                "example": "d4f1a2b3-1000-0000-0000-000000000001",
                "x-parser-schema-id": "<anonymous-schema-5>"
              },
              "senderId": {
                "type": "string",
                "description": "The sender's UserIdentifier resolved server-side.",
                "example": "d4f1a2b3-0000-0000-0000-000000000001",
                "x-parser-schema-id": "<anonymous-schema-6>"
              },
              "content": {
                "type": "string",
                "description": "The plain-text message content.",
                "example": "Hello everyone!",
                "x-parser-schema-id": "<anonymous-schema-7>"
              },
              "sentAt": {
                "type": "string",
                "format": "date-time",
                "description": "UTC timestamp when the message was sent.",
                "example": "2026-03-09T12:00:00Z",
                "x-parser-schema-id": "<anonymous-schema-8>"
              }
            },
            "x-parser-schema-id": "ReceiveMessagePayload"
          },
          "examples": [
            {
              "name": "IncomingMessage",
              "summary": "Message delivered to the chat group",
              "payload": {
                "id": "f1a2b3c4-0000-0000-0000-000000000001",
                "chatId": "d4f1a2b3-1000-0000-0000-000000000001",
                "senderId": "d4f1a2b3-0000-0000-0000-000000000001",
                "content": "Hello everyone!",
                "sentAt": "2026-03-09T12:00:00Z"
              }
            }
          ]
        }
      }
    }
  },
  "components": {
    "messages": {
      "JoinChatRequest": "$ref:$.channels.chat/joinChat.publish.message",
      "SendMessageRequest": "$ref:$.channels.chat/sendMessage.publish.message",
      "ReceiveMessageEvent": "$ref:$.channels.chat/receiveMessage.subscribe.message"
    },
    "schemas": {
      "JoinChatPayload": "$ref:$.channels.chat/joinChat.publish.message.payload",
      "SendMessagePayload": "$ref:$.channels.chat/sendMessage.publish.message.payload",
      "ReceiveMessagePayload": "$ref:$.channels.chat/receiveMessage.subscribe.message.payload"
    },
    "securitySchemes": {
      "bearerAuth": {
        "type": "http",
        "scheme": "bearer",
        "bearerFormat": "JWT",
        "description": "Standard JWT bearer token. Connections without a resolved UserIdentifier are aborted immediately in OnConnectedAsync.\n"
      }
    }
  },
  "x-parser-spec-parsed": true,
  "x-parser-api-version": 3,
  "x-parser-spec-stringified": true
};
    const config = {"show":{"sidebar":true},"sidebar":{"showOperations":"byDefault"}};
    const appRoot = document.getElementById('root');
    AsyncApiStandalone.render(
        { schema, config, }, appRoot
    );
  