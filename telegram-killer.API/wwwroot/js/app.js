
    const schema = {
  "asyncapi": "2.6.0",
  "info": {
    "title": "ChatHub API",
    "version": "2.1.0",
    "description": "Real-time chat API powered by ASP.NET Core SignalR over WebSockets.\n\n## Authentication\nAll connections must carry a valid JWT bearer token. Connections that\narrive without a resolved UserIdentifier are immediately aborted by the server.\n\n## Connection Lifecycle\n- OnConnectedAsync: Validates UserIdentifier; missing aborts connection.\n- Active: Client may invoke JoinChat, LeaveChat, SendMessage, MarkAsRead.\n- OnDisconnectedAsync: Logs normal or error-caused disconnection.\n"
  },
  "servers": {
    "development": {
      "url": "ws://localhost:8080/hub/chat",
      "protocol": "ws",
      "security": [
        {
          "bearerAuth": []
        }
      ]
    }
  },
  "channels": {
    "chat/joinChat": {
      "publish": {
        "operationId": "joinChat",
        "summary": "Join a chat group",
        "message": {
          "name": "JoinChat",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId"
            ],
            "properties": {
              "chatId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-1>"
              }
            },
            "x-parser-schema-id": "JoinChatPayload"
          }
        }
      }
    },
    "chat/leaveChat": {
      "publish": {
        "operationId": "leaveChat",
        "summary": "Leave a chat group",
        "message": {
          "name": "LeaveChat",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId"
            ],
            "properties": {
              "chatId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-2>"
              }
            },
            "x-parser-schema-id": "LeaveChatPayload"
          }
        }
      }
    },
    "chat/sendMessage": {
      "publish": {
        "operationId": "sendMessage",
        "summary": "Send a message to a chat group",
        "message": {
          "name": "SendMessage",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId",
              "content"
            ],
            "properties": {
              "chatId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-3>"
              },
              "content": {
                "type": "string",
                "minLength": 1,
                "x-parser-schema-id": "<anonymous-schema-4>"
              }
            },
            "x-parser-schema-id": "SendMessagePayload"
          }
        }
      }
    },
    "chat/markAsRead": {
      "publish": {
        "operationId": "markAsRead",
        "summary": "Mark a message as read",
        "message": {
          "name": "MarkAsRead",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId",
              "messageId"
            ],
            "properties": {
              "chatId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-5>"
              },
              "messageId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-6>"
              }
            },
            "x-parser-schema-id": "MarkAsReadPayload"
          }
        }
      }
    },
    "chat/receiveMessage": {
      "subscribe": {
        "operationId": "receiveMessage",
        "summary": "Receive a message from a chat group",
        "message": {
          "name": "ReceiveMessage",
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
            "properties": {
              "id": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-7>"
              },
              "chatId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-8>"
              },
              "senderId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-9>"
              },
              "content": {
                "type": "string",
                "x-parser-schema-id": "<anonymous-schema-10>"
              },
              "sentAt": {
                "type": "string",
                "format": "date-time",
                "x-parser-schema-id": "<anonymous-schema-11>"
              }
            },
            "x-parser-schema-id": "ReceiveMessagePayload"
          }
        }
      }
    }
  },
  "components": {
    "messages": {
      "JoinChatRequest": "$ref:$.channels.chat/joinChat.publish.message",
      "LeaveChatRequest": "$ref:$.channels.chat/leaveChat.publish.message",
      "SendMessageRequest": "$ref:$.channels.chat/sendMessage.publish.message",
      "MarkAsReadRequest": "$ref:$.channels.chat/markAsRead.publish.message",
      "ReceiveMessageEvent": "$ref:$.channels.chat/receiveMessage.subscribe.message"
    },
    "schemas": {
      "JoinChatPayload": "$ref:$.channels.chat/joinChat.publish.message.payload",
      "LeaveChatPayload": "$ref:$.channels.chat/leaveChat.publish.message.payload",
      "SendMessagePayload": "$ref:$.channels.chat/sendMessage.publish.message.payload",
      "MarkAsReadPayload": "$ref:$.channels.chat/markAsRead.publish.message.payload",
      "ReceiveMessagePayload": "$ref:$.channels.chat/receiveMessage.subscribe.message.payload"
    },
    "securitySchemes": {
      "bearerAuth": {
        "type": "http",
        "scheme": "bearer",
        "bearerFormat": "JWT"
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
  