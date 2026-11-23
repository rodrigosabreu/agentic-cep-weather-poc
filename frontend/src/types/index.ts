export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'tool';
  content: string;
  timestamp: Date;
  toolCalls?: ToolCall[];
  toolCallId?: string;
  toolResult?: ToolResult;
}

export interface ToolCall {
  id: string;
  name: string;
  arguments: string;
}

export interface ToolResult {
  toolCallId: string;
  name: string;
  result: string;
  success: boolean;
  error?: string;
}

export interface ChatSession {
  id: string;
  messages: ChatMessage[];
  createdAt: Date | string;
  updatedAt: Date | string;
}

export interface ToolDefinition {
  name: string;
  description: string;
  parameters: JsonSchema;
}

export interface JsonSchema {
  type: string;
  properties?: Record<string, JsonSchemaProperty>;
  required?: string[];
}

export interface JsonSchemaProperty {
  type: string;
  description?: string;
  enum?: string[];
}

export interface SendMessageRequest {
  sessionId: string;
  message: string;
}

export interface SendMessageResponse {
  message: string;
  toolResults: ToolResult[];
  sessionId: string;
}

export interface SessionResponse {
  id: string;
  messages: ChatMessage[];
  createdAt: string;
  updatedAt: string;
}

export interface ApiError {
  error: string;
  statusCode?: number;
}

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';