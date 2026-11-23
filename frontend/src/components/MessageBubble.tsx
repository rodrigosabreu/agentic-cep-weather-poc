import React from 'react';
import type { ChatMessage, ToolResult } from '@/types';

interface MessageBubbleProps {
  message: ChatMessage;
  isStreaming?: boolean;
}

const formatTimestamp = (date: Date): string => {
  return new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
};

const getToolDisplayName = (toolName: string): string => {
  switch (toolName) {
    case 'consultar_cep':
      return 'Consulta CEP';
    case 'consultar_previsao_tempo':
      return 'Previsão do Tempo';
    default:
      return toolName;
  }
};

const parseToolResult = (result: ToolResult): { title: string; data: Record<string, unknown> } | null => {
  try {
    const parsed = JSON.parse(result.result);
    return { title: getToolDisplayName(result.name), data: parsed };
  } catch {
    return null;
  }
};

const ToolResultDisplay: React.FC<{ result: ToolResult }> = ({ result }) => {
  const parsed = parseToolResult(result);
  
  if (!parsed) {
    return (
      <div className={`flex items-start gap-2 p-3 rounded-lg text-sm ${
        result.success ? 'bg-green-50 border border-green-200' : 'bg-red-50 border border-red-200'
      }`}>
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-1">
            <span className={`font-medium text-xs ${result.success ? 'text-green-800' : 'text-red-800'}`}>
              {getToolDisplayName(result.name)}
            </span>
            <span className={`px-1.5 py-0.5 rounded text-xs font-medium ${
              result.success ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'
            }`}>
              {result.success ? 'Sucesso' : 'Erro'}
            </span>
          </div>
          <pre className="whitespace-pre-wrap text-xs text-gray-600 bg-gray-100 p-2 rounded">
            {result.success ? result.result : (result.error || 'Erro desconhecido')}
          </pre>
        </div>
      </div>
    );
  }

  const { title, data } = parsed;
  const entries = Object.entries(data).filter(([_, v]) => v !== '' && v !== null && v !== undefined);

  return (
    <div className="flex items-start gap-2 p-3 rounded-lg bg-blue-50 border border-blue-200 animate-slide-up" role="region" aria-label={`Resultado da ferramenta: ${title}`}>
      <div className="flex-shrink-0 mt-0.5">
        <svg className="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-2">
          <span className="font-medium text-sm text-blue-800">{title}</span>
          <span className="px-1.5 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-700">
            {result.success ? 'Concluído' : 'Erro'}
          </span>
        </div>
        <div className="grid gap-1 text-sm">
          {entries.map(([key, value]) => (
            <div key={key} className="flex gap-2">
              <span className="font-medium text-blue-700 w-32 flex-shrink-0 truncate">
                {key.charAt(0).toUpperCase() + key.slice(1).replace(/([A-Z])/g, ' $1')}:
              </span>
              <span className="text-blue-800 truncate">{String(value)}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

const ToolCallsDisplay: React.FC<{ toolCalls: ChatMessage['toolCalls'] }> = ({ toolCalls }) => {
  if (!toolCalls || toolCalls.length === 0) return null;

  return (
    <div className="mt-2 space-y-1" role="region" aria-label="Ferramentas sendo executadas">
      {toolCalls.map((tc) => (
        <div key={tc.id} className="flex items-center gap-2 p-2 rounded-lg bg-yellow-50 border border-yellow-200 animate-slide-up">
          <div className="flex items-center gap-1.5 text-yellow-700">
            <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" aria-hidden="true">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
            </svg>
            <span className="text-sm font-medium">Executando {getToolDisplayName(tc.name)}...</span>
          </div>
        </div>
      ))}
    </div>
  );
};

export const MessageBubble: React.FC<MessageBubbleProps> = ({ message, isStreaming }) => {
  const isUser = message.role === 'user';
  const isTool = message.role === 'tool';
  const isAssistant = message.role === 'assistant';

  if (isTool) {
    return (
      <div className="flex justify-start animate-fade-in" role="log" aria-live="polite">
        <div className="max-w-[85%]">
          <ToolResultDisplay result={message.toolResult!} />
        </div>
      </div>
    );
  }

  return (
    <div className={`flex animate-fade-in ${isUser ? 'justify-end' : 'justify-start'}`} role="log" aria-live="polite">
      <div className={`max-w-[85%] ${isUser ? 'order-2' : ''}`}>
        {!isUser && (
          <div className="flex items-center gap-1.5 mb-1 ml-1">
            <div className="w-6 h-6 rounded-full bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center flex-shrink-0">
              <svg className="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h16M8 12l-5 5M8 12l-5-5" />
              </svg>
            </div>
            <span className="text-xs font-medium text-gray-500">Assistente</span>
            <time className="text-xs text-gray-400" dateTime={message.timestamp.toISOString()}>
              {formatTimestamp(message.timestamp)}
            </time>
          </div>
        )}

        <div
          className={`relative rounded-2xl px-4 py-3 ${
            isUser
              ? 'bg-blue-600 text-white rounded-br-md'
              : 'bg-white text-gray-900 shadow-sm border border-gray-100 rounded-bl-md'
          }`}
        >
          <div className="whitespace-pre-wrap text-base leading-relaxed">
            {message.content || (isStreaming && isAssistant ? '▋' : '')}
          </div>
          
          {isStreaming && isAssistant && (
            <div className="absolute bottom-1 right-2 flex items-center gap-1 text-blue-400 text-xs">
              <span className="animate-pulse">●</span>
              <span>Digitando...</span>
            </div>
          )}

          {message.toolCalls && message.toolCalls.length > 0 && (
            <ToolCallsDisplay toolCalls={message.toolCalls} />
          )}

          {message.toolResult && (
            <ToolResultDisplay result={message.toolResult} />
          )}
        </div>

        {isUser && (
          <div className="flex items-center gap-1.5 mb-1 mr-1 justify-end">
            <time className="text-xs text-gray-400" dateTime={message.timestamp.toISOString()}>
              {formatTimestamp(message.timestamp)}
            </time>
            <span className="text-xs font-medium text-gray-500">Você</span>
            <div className="w-6 h-6 rounded-full bg-gray-200 flex items-center justify-center flex-shrink-0">
              <svg className="w-4 h-4 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
              </svg>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default MessageBubble;