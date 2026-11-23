import { useEffect, useRef } from 'react';
import { useChat } from '@/hooks/useChat';
import { MessageBubble } from '@/components/MessageBubble';
import { InputArea } from '@/components/InputArea';

function App() {
  const {
    messages,
    isLoading,
    isStreaming,
    error,
    newChat,
    sendMessageStream,
    abortCurrentRequest,
    clearError,
    setConnectionStatus,
  } = useChat();

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputAreaRef = useRef<{ focus: () => void; clear: () => void }>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages, isStreaming]);

  useEffect(() => {
    const checkHealth = async () => {
      try {
        const healthy = await fetch('/api/health').then(r => r.ok);
        setConnectionStatus(healthy ? 'connected' : 'error');
      } catch {
        setConnectionStatus('error');
      }
    };
    checkHealth();
    const interval = setInterval(checkHealth, 30000);
    return () => clearInterval(interval);
  }, [setConnectionStatus]);

  const handleSend = (message: string) => {
    sendMessageStream(message);
  };

  const handleNewChat = () => {
    newChat();
    inputAreaRef.current?.clear();
    inputAreaRef.current?.focus();
  };

  const handleAbort = () => {
    abortCurrentRequest();
    inputAreaRef.current?.focus();
  };

  const handleRetry = () => {
    clearError();
    if (messages.length > 0) {
      const lastUserMessage = [...messages].reverse().find(m => m.role === 'user');
      if (lastUserMessage) {
        sendMessageStream(lastUserMessage.content);
      }
    }
  };

  return (
    <div className="flex h-screen w-full flex-col bg-gray-50">
      <header className="flex items-center justify-between px-4 py-3 bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center flex-shrink-0">
            <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h16M8 12l-5 5M8 12l-5-5" />
            </svg>
          </div>
          <div>
            <h1 className="text-lg font-semibold text-gray-900">Agentic CEP Weather</h1>
            <p className="text-xs text-gray-500">Chat com IA • CEP + Previsão do Tempo</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleNewChat}
            className="px-3 py-1.5 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors"
            aria-label="Nova conversa"
          >
            <svg className="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Nova Conversa
          </button>
        </div>
      </header>

      <main className="flex-1 overflow-hidden flex flex-col">
        {error && (
          <div className="mx-4 mt-3 p-3 bg-red-50 border border-red-200 rounded-lg flex items-start gap-3 animate-slide-up" role="alert">
            <svg className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <div className="flex-1">
              <p className="text-sm text-red-800">{error}</p>
              <button
                onClick={handleRetry}
                className="mt-2 text-xs font-medium text-red-600 hover:text-red-700 underline"
              >
                Tentar novamente
              </button>
            </div>
            <button
              onClick={clearError}
              className="text-red-400 hover:text-red-600 flex-shrink-0"
              aria-label="Fechar erro"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        )}

        <div 
          className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-thin"
          role="log"
          aria-live="polite"
          aria-label="Histórico da conversa"
        >
          {messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-center text-gray-500 py-12">
              <div className="w-16 h-16 rounded-full bg-blue-50 flex items-center justify-center mb-4">
                <svg className="w-8 h-8 text-blue-500" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h16M8 12l-5 5M8 12l-5-5" />
                </svg>
              </div>
              <h2 className="text-lg font-medium text-gray-900 mb-1">Bem-vindo ao Agentic CEP Weather!</h2>
              <p className="text-sm max-w-xs">
                Pergunte sobre CEPs ou previsão do tempo. Exemplos:
              </p>
              <div className="mt-3 space-y-2 text-xs text-gray-400 max-w-xs">
                <div className="bg-white p-3 rounded-lg border border-gray-100 text-left">
                  <code>"Qual o CEP 01310-100?"</code>
                </div>
                <div className="bg-white p-3 rounded-lg border border-gray-100 text-left">
                  <code>"Como está o tempo em São Paulo/SP?"</code>
                </div>
                <div className="bg-white p-3 rounded-lg border border-gray-100 text-left">
                  <code>"Qual o tempo no CEP 20040-002?"</code>
                </div>
              </div>
            </div>
          ) : (
            <>
              {messages.map((message) => (
                <MessageBubble
                  key={message.id}
                  message={message}
                  isStreaming={isStreaming}
                />
              ))}
              <div ref={messagesEndRef} />
            </>
          )}
        </div>

        <InputArea
          ref={inputAreaRef}
          onSend={handleSend}
          disabled={isLoading || isStreaming || messages.length === 0 && isLoading}
          streaming={isStreaming}
          onAbort={handleAbort}
          placeholder={messages.length === 0 ? 'Digite sua mensagem para começar...' : 'Digite sua mensagem...'}
        />
      </main>
    </div>
  );
}

export default App;