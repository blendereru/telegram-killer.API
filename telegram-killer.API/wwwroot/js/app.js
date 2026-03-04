
    const schema = {
  "asyncapi": "2.6.0",
  "info": {
    "title": "ChatHub API",
    "version": "1.0.0",
    "description": "Real-time chat API powered by ASP.NET Core SignalR over WebSockets.\n\n## Authentication\nAll connections must carry a valid JWT bearer token. Connections that\narrive without a resolved UserIdentifier are immediately aborted by the\nserver (Context.Abort()).\n\n## Connection Lifecycle\n- OnConnectedAsync: Server validates UserIdentifier. Missing means connection is aborted.\n- Active: Client may invoke SendMessage; server pushes ReceiveMessage.\n- OnDisconnectedAsync: Server logs normal or error-caused disconnection.\n"
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
    "chat/sendMessage": {
      "description": "The client invokes this channel to dispatch a private message to another\nconnected user. Maps directly to the SendMessage(string to, string message)\nhub method. The caller's UserIdentifier is resolved server-side from the JWT\nclaims so the \"from\" field is not part of the request payload.\n",
      "publish": {
        "operationId": "sendMessage",
        "summary": "Send a private message to a specific user",
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
              "to",
              "message"
            ],
            "additionalProperties": false,
            "properties": {
              "to": {
                "type": "string",
                "minLength": 1,
                "description": "The UserIdentifier of the intended recipient. Must match the NameIdentifier claim value stored in the server connection mapping.\n",
                "example": "d4f1a2b3-0001-0001-0001-000000000001",
                "x-parser-schema-id": "<anonymous-schema-1>"
              },
              "message": {
                "type": "string",
                "minLength": 1,
                "description": "Plain-text content of the message to deliver.",
                "example": "Hey, are you free for a call?",
                "x-parser-schema-id": "<anonymous-schema-2>"
              }
            },
            "x-parser-schema-id": "SendMessagePayload"
          },
          "examples": [
            {
              "name": "BasicMessage",
              "summary": "A simple text message sent to another user",
              "payload": {
                "to": "d4f1a2b3-0001-0001-0001-000000000001",
                "message": "Hey, are you free for a call?"
              }
            }
          ]
        }
      }
    },
    "chat/receiveMessage": {
      "description": "The server pushes this event to the target user identified by the \"to\" field\nof the originating SendMessage invocation. Maps to the ReceiveMessage\nclient-side handler invoked via Clients.User(to).SendAsync(\"ReceiveMessage\", from, message).\n",
      "subscribe": {
        "operationId": "receiveMessage",
        "summary": "Receive a private message from another user",
        "tags": [
          {
            "name": "messaging"
          }
        ],
        "message": {
          "name": "ReceiveMessage",
          "title": "Receive Message Event",
          "summary": "Payload the server pushes to the target user",
          "contentType": "application/json",
          "payload": {
            "type": "object",
            "required": [
              "from",
              "message"
            ],
            "additionalProperties": false,
            "properties": {
              "from": {
                "type": "string",
                "minLength": 1,
                "description": "The UserIdentifier of the sender. Populated server-side from Context.UserIdentifier and cannot be forged by the client.\n",
                "example": "d4f1a2b3-0000-0000-0000-000000000001",
                "x-parser-schema-id": "<anonymous-schema-3>"
              },
              "message": {
                "type": "string",
                "minLength": 1,
                "description": "Plain-text content of the received message.",
                "example": "Hey, are you free for a call?",
                "x-parser-schema-id": "<anonymous-schema-4>"
              }
            },
            "x-parser-schema-id": "ReceiveMessagePayload"
          },
          "examples": [
            {
              "name": "IncomingMessage",
              "summary": "A message delivered to the recipient",
              "payload": {
                "from": "d4f1a2b3-0000-0000-0000-000000000001",
                "message": "Hey, are you free for a call?"
              }
            }
          ]
        }
      }
    }
  },
  "components": {
    "messages": {
      "SendMessageRequest": "$ref:$.channels.chat/sendMessage.publish.message",
      "ReceiveMessageEvent": "$ref:$.channels.chat/receiveMessage.subscribe.message"
    },
    "schemas": {
      "SendMessagePayload": "$ref:$.channels.chat/sendMessage.publish.message.payload",
      "ReceiveMessagePayload": "$ref:$.channels.chat/receiveMessage.subscribe.message.payload"
    },
    "securitySchemes": {
      "bearerAuth": {
        "type": "http",
        "scheme": "bearer",
        "bearerFormat": "JWT",
        "description": "Standard JWT bearer token. The server resolves Context.UserIdentifier from the token NameIdentifier claim. Connections that do not yield a UserIdentifier are aborted immediately in OnConnectedAsync.\n"
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
  