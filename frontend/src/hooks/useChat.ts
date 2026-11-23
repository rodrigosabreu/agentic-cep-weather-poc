import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import type { ChatMessage, ToolResult, ConnectionStatus } from '@/types';
import { apiClient } from '@/api/client';
import { v4 as uuidv4 } from 'uuid';

interface StoredChatSession {
  id: string;
  messages: StoredChatMessage[];
  createdAt: string;
  updatedAt: string;
}

interface StoredChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'tool';
  content: string;
  timestamp: string;
  toolCalls?: StoredToolCall[];
  toolCallId?: string;
  toolResult?: ToolResult;
}

interface StoredToolCall {
  id: string;
  name: string;
  arguments: string;
}

interface ChatState {
  currentSessionId: string | null;
  sessions: Record<string, StoredChatSession>;
  messages: ChatMessage[];
  isLoading: boolean;
  isStreaming: boolean;
  connectionStatus: ConnectionStatus;
  error: string | null;
  abortController: AbortController | null;

  setConnectionStatus: (status: ConnectionStatus) => void;
  setError: (error: string | null) => void;
  clearError: () => void;

  createSession: () => string;
  setSession: (sessionId: string) => void;
  loadSession: (sessionId: string) => Promise<void>;
  deleteSession: (sessionId: string) => Promise<void>;
  newChat: () => string;

  sendMessage: (message: string) => Promise<void>;
  sendMessageStream: (message: string) => Promise<void>;
  abortCurrentRequest: () => void;

  addMessage: (message: ChatMessage) => void;
  updateLastMessage: (content: string) => void;
  appendToLastMessage: (content: string) => void;
  setToolResults: (toolResults: ToolResult[]) => void;

  setLoading: (loading: boolean) => void;
  setStreaming: (streaming: boolean) => void;
}

function createEmptySession(sessionId: string): StoredChatSession {
  return {
    id: sessionId,
    messages: [],
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
}

function storedToChatMessage(stored: StoredChatMessage): ChatMessage {
  return {
    ...stored,
    timestamp: new Date(stored.timestamp),
    toolCalls: stored.toolCalls?.map(tc => ({ ...tc })),
  };
}

function chatToStoredMessage(chat: ChatMessage): StoredChatMessage {
  return {
    ...chat,
    timestamp: chat.timestamp.toISOString(),
    toolCalls: chat.toolCalls?.map(tc => ({ ...tc })),
  };
}

function storedSessionToMessages(session: StoredChatSession): ChatMessage[] {
  return session.messages.map(storedToChatMessage);
}

function messagesToStored(messages: ChatMessage[]): StoredChatMessage[] {
  return messages.map(chatToStoredMessage);
}

export const useChatStore = create<ChatState>()(
  persist(
    (set, get) => ({
      currentSessionId: null,
      sessions: {},
      messages: [],
      isLoading: false,
      isStreaming: false,
      connectionStatus: 'disconnected',
      error: null,
      abortController: null,

      setConnectionStatus: (status) => set({ connectionStatus: status }),
      setError: (error) => set({ error }),
      clearError: () => set({ error: null }),

      createSession: () => {
        const sessionId = uuidv4();
        const session = createEmptySession(sessionId);
        set(state => ({
          sessions: { ...state.sessions, [sessionId]: session },
          currentSessionId: sessionId,
          messages: [],
        }));
        return sessionId;
      },

      setSession: (sessionId) => {
        const session = get().sessions[sessionId];
        if (session) {
          set({
            currentSessionId: sessionId,
            messages: storedSessionToMessages(session),
          });
          apiClient.setSessionId(sessionId);
        }
      },

      loadSession: async (sessionId) => {
        set({ isLoading: true, error: null });
        try {
          const sessionData = await apiClient.getSession(sessionId);
          const storedSession: StoredChatSession = {
            id: sessionData.id,
            messages: sessionData.messages.map(m => ({
              ...m,
              timestamp: m.timestamp instanceof Date ? m.timestamp.toISOString() : m.timestamp,
              toolCalls: m.toolCalls?.map(tc => ({ ...tc })),
            })),
            createdAt: sessionData.createdAt,
            updatedAt: sessionData.updatedAt,
          };
          set(state => ({
            sessions: { ...state.sessions, [sessionId]: storedSession },
            currentSessionId: sessionId,
            messages: storedSessionToMessages(storedSession),
            isLoading: false,
          }));
          apiClient.setSessionId(sessionId);
        } catch (error) {
          set({ 
            error: error instanceof Error ? error.message : 'Erro ao carregar sessão',
            isLoading: false 
          });
        }
      },

      deleteSession: async (sessionId) => {
        try {
          await apiClient.deleteSession(sessionId);
          set(state => {
            const newSessions = { ...state.sessions };
            delete newSessions[sessionId];
            let newCurrentId = state.currentSessionId;
            let newMessages = state.messages;
            
            if (state.currentSessionId === sessionId) {
              const remainingSessions = Object.values(newSessions);
              if (remainingSessions.length > 0) {
                newCurrentId = remainingSessions[0].id;
                newMessages = storedSessionToMessages(remainingSessions[0]);
              } else {
                newCurrentId = null;
                newMessages = [];
              }
            }
            
            return {
              sessions: newSessions,
              currentSessionId: newCurrentId,
              messages: newMessages,
            };
          });
          
          if (get().currentSessionId) {
            apiClient.setSessionId(get().currentSessionId!);
          } else {
            apiClient.clearSessionId();
          }
        } catch (error) {
          set({ error: error instanceof Error ? error.message : 'Erro ao excluir sessão' });
        }
      },

      newChat: () => {
        const sessionId = get().createSession();
        apiClient.setSessionId(sessionId);
        return sessionId;
      },

      sendMessage: async (message) => {
        const { currentSessionId, messages } = get();
        if (!currentSessionId || !message.trim()) return;

        const userMessage: ChatMessage = {
          id: uuidv4(),
          role: 'user',
          content: message.trim(),
          timestamp: new Date(),
        };

        set({ 
          messages: [...messages, userMessage],
          isLoading: true,
          error: null,
        });

        try {
          const response = await apiClient.sendMessage({
            sessionId: currentSessionId,
            message: message.trim(),
          });

          const assistantMessage: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content: response.message,
            timestamp: new Date(),
            toolResult: response.toolResults[0],
          };

          set(state => {
            const updatedMessages = [...state.messages, assistantMessage];
            const session = state.sessions[currentSessionId];
            return {
              messages: updatedMessages,
              isLoading: false,
              sessions: {
                ...state.sessions,
                [currentSessionId]: {
                  ...session!,
                  messages: messagesToStored(updatedMessages),
                  updatedAt: new Date().toISOString(),
                },
              },
            };
          });
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : 'Erro ao enviar mensagem';
          set({ 
            error: errorMessage,
            isLoading: false,
            messages: messages,
          });
        }
      },

      sendMessageStream: async (message) => {
        const { currentSessionId, messages } = get();
        if (!currentSessionId || !message.trim()) return;

        const userMessage: ChatMessage = {
          id: uuidv4(),
          role: 'user',
          content: message.trim(),
          timestamp: new Date(),
        };

        set({ 
          messages: [...messages, userMessage],
          isStreaming: true,
          error: null,
        });

        const controller = new AbortController();
        set({ abortController: controller });

        let accumulatedContent = '';
        let assistantMessageId = uuidv4();

        const updateAssistantMessage = (content: string) => {
          set(state => ({
            messages: state.messages.map(m => 
              m.id === assistantMessageId ? { ...m, content } : m
            ),
          }));
        };

        try {
          await apiClient.sendMessageStream(
            { sessionId: currentSessionId, message: message.trim() },
            (chunk) => {
              accumulatedContent += chunk;
              updateAssistantMessage(accumulatedContent);
            },
            (response) => {
              const assistantMessage: ChatMessage = {
                id: assistantMessageId,
                role: 'assistant',
                content: response.message || accumulatedContent,
                timestamp: new Date(),
                toolResult: response.toolResults[0],
              };

              set(state => {
                const updatedMessages = state.messages.map(m => 
                  m.id === assistantMessageId ? assistantMessage : m
                );
                const session = state.sessions[currentSessionId];
                return {
                  messages: updatedMessages,
                  isStreaming: false,
                  abortController: null,
                  sessions: {
                    ...state.sessions,
                    [currentSessionId]: {
                      ...session!,
                      messages: messagesToStored(updatedMessages),
                      updatedAt: new Date().toISOString(),
                    },
                  },
                };
              });
            },
            (error) => {
              set({ 
                error: error.message,
                isStreaming: false,
                abortController: null,
                messages: messages,
              });
            }
          );
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : 'Erro no streaming';
          set({ 
            error: errorMessage,
            isStreaming: false,
            abortController: null,
            messages: messages,
          });
        }
      },

      abortCurrentRequest: () => {
        const { currentSessionId, abortController } = get();
        if (abortController) {
          abortController.abort();
        }
        if (currentSessionId) {
          apiClient.abortRequest(currentSessionId);
        }
        set({ isStreaming: false, abortController: null });
      },

      addMessage: (message) => {
        set(state => ({
          messages: [...state.messages, message],
        }));
      },

      updateLastMessage: (content) => {
        set(state => {
          if (state.messages.length === 0) return state;
          const newMessages = [...state.messages];
          newMessages[newMessages.length - 1] = {
            ...newMessages[newMessages.length - 1],
            content,
          };
          return { messages: newMessages };
        });
      },

      appendToLastMessage: (content) => {
        set(state => {
          if (state.messages.length === 0) return state;
          const newMessages = [...state.messages];
          const lastIndex = newMessages.length - 1;
          newMessages[lastIndex] = {
            ...newMessages[lastIndex],
            content: newMessages[lastIndex].content + content,
          };
          return { messages: newMessages };
        });
      },

      setToolResults: (toolResults) => {
        set(state => {
          if (state.messages.length === 0) return state;
          const newMessages = [...state.messages];
          const lastIndex = newMessages.length - 1;
          if (newMessages[lastIndex].role === 'assistant') {
            newMessages[lastIndex] = {
              ...newMessages[lastIndex],
              toolResult: toolResults[0],
            };
          }
          return { messages: newMessages };
        });
      },

      setLoading: (loading) => set({ isLoading: loading }),
      setStreaming: (streaming) => set({ isStreaming: streaming }),
    }),
    {
      name: 'agentic-cep-weather-chat',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        currentSessionId: state.currentSessionId,
        sessions: state.sessions,
      }),
      onRehydrateStorage: () => (state) => {
        if (state) {
          if (state.currentSessionId) {
            const session = state.sessions[state.currentSessionId];
            if (session) {
              state.messages = storedSessionToMessages(session);
              apiClient.setSessionId(state.currentSessionId);
            }
          }
        }
      },
    }
  )
);

export const useChat = () => {
  const store = useChatStore();
  
  return {
    currentSessionId: store.currentSessionId,
    messages: store.messages,
    isLoading: store.isLoading,
    isStreaming: store.isStreaming,
    connectionStatus: store.connectionStatus,
    error: store.error,
    
    createSession: store.createSession,
    setSession: store.setSession,
    loadSession: store.loadSession,
    deleteSession: store.deleteSession,
    newChat: store.newChat,
    
    sendMessage: store.sendMessage,
    sendMessageStream: store.sendMessageStream,
    abortCurrentRequest: store.abortCurrentRequest,
    
    setError: store.setError,
    clearError: store.clearError,
    setConnectionStatus: store.setConnectionStatus,
  };
};

export default useChat;