
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
    "chat/JoinChat": {
      "publish": {
        "operationId": "JoinChat",
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
    "chat/LeaveChat": {
      "publish": {
        "operationId": "LeaveChat",
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
    "chat/SendMessage": {
      "publish": {
        "operationId": "SendMessage",
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
    "chat/MarkAsRead": {
      "publish": {
        "operationId": "MarkAsRead",
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
    "chat/ReceiveMessage": {
      "subscribe": {
        "operationId": "ReceiveMessage",
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
    },
    "chat/MessageRead": {
      "subscribe": {
        "operationId": "MessageRead",
        "summary": "Receive notification that a message was read",
        "message": {
          "name": "MessageRead",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "chatId",
              "messageId",
              "userId",
              "readAt"
            ],
            "properties": {
              "chatId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-12>"
              },
              "messageId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-13>"
              },
              "userId": {
                "type": "string",
                "format": "uuid",
                "x-parser-schema-id": "<anonymous-schema-14>"
              },
              "readAt": {
                "type": "string",
                "format": "date-time",
                "x-parser-schema-id": "<anonymous-schema-15>"
              }
            },
            "x-parser-schema-id": "MessageReadPayload"
          }
        }
      }
    }
  },
  "components": {
    "messages": {
      "JoinChatRequest": "$ref:$.channels.chat/JoinChat.publish.message",
      "LeaveChatRequest": "$ref:$.channels.chat/LeaveChat.publish.message",
      "SendMessageRequest": "$ref:$.channels.chat/SendMessage.publish.message",
      "MarkAsReadRequest": "$ref:$.channels.chat/MarkAsRead.publish.message",
      "ReceiveMessageEvent": "$ref:$.channels.chat/ReceiveMessage.subscribe.message",
      "MessageReadEvent": "$ref:$.channels.chat/MessageRead.subscribe.message"
    },
    "schemas": {
      "JoinChatPayload": "$ref:$.channels.chat/JoinChat.publish.message.payload",
      "LeaveChatPayload": "$ref:$.channels.chat/LeaveChat.publish.message.payload",
      "SendMessagePayload": "$ref:$.channels.chat/SendMessage.publish.message.payload",
      "MarkAsReadPayload": "$ref:$.channels.chat/MarkAsRead.publish.message.payload",
      "ReceiveMessagePayload": "$ref:$.channels.chat/ReceiveMessage.subscribe.message.payload",
      "MessageReadPayload": "$ref:$.channels.chat/MessageRead.subscribe.message.payload"
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
  