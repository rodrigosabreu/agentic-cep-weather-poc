import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import type { ToolResult, SessionResponse, ToolDefinition, SendMessageRequest, SendMessageResponse, ApiError } from '@/types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

class ApiClient {
  private client: AxiosInstance;
  private abortControllers: Map<string, AbortController> = new Map();

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      timeout: 60000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.client.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        const sessionId = config.headers?.['X-Session-Id'] as string;
        if (sessionId) {
          config.headers['X-Session-Id'] = sessionId;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError<ApiError>) => {
        if (error.code === 'ECONNABORTED') {
          return Promise.reject(new Error('Timeout: A requisição demorou demais para responder'));
        }
        
        if (!error.response) {
          return Promise.reject(new Error('Erro de conexão: Verifique se o backend está rodando'));
        }

        const message = error.response.data?.error || 
                       `Erro ${error.response.status}: ${error.response.statusText}`;
        return Promise.reject(new Error(message));
      }
    );
  }

  setSessionId(sessionId: string) {
    this.client.defaults.headers.common['X-Session-Id'] = sessionId;
  }

  clearSessionId() {
    delete this.client.defaults.headers.common['X-Session-Id'];
  }

  async sendMessage(request: SendMessageRequest): Promise<SendMessageResponse> {
    const response = await this.client.post<SendMessageResponse>('/chat', request);
    return response.data;
  }

  async sendMessageStream(
    request: SendMessageRequest,
    onChunk: (chunk: string) => void,
    onComplete: (data: SendMessageResponse) => void,
    onError: (error: Error) => void
  ): Promise<void> {
    const controller = new AbortController();
    const requestKey = `${request.sessionId}-${Date.now()}`;
    this.abortControllers.set(requestKey, controller);

    try {
      const response = await fetch(`${API_BASE_URL}/chat/stream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Session-Id': request.sessionId,
        },
        body: JSON.stringify(request),
        signal: controller.signal,
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.error || `Erro ${response.status}: ${response.statusText}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('Não foi possível ler o stream de resposta');
      }

      const decoder = new TextDecoder();
      let buffer = '';
      let accumulatedContent = '';
      let accumulatedToolResults: ToolResult[] = [];
      let isComplete = false;

      while (!isComplete) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            try {
              const data = JSON.parse(line.slice(6));
              
              if (data.contentDelta) {
                accumulatedContent += data.contentDelta;
                onChunk(data.contentDelta);
              }

              if (data.toolResults && data.toolResults.length > 0) {
                accumulatedToolResults = data.toolResults;
              }

              if (data.isComplete) {
                isComplete = true;
                onComplete({
                  message: accumulatedContent,
                  toolResults: accumulatedToolResults,
                  sessionId: request.sessionId,
                });
              }
            } catch {
              // Ignora linhas malformadas
            }
          }
        }
      }
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        return;
      }
      onError(error instanceof Error ? error : new Error('Erro desconhecido no stream'));
    } finally {
      this.abortControllers.delete(requestKey);
    }
  }

  abortRequest(sessionId: string) {
    const keysToAbort = Array.from(this.abortControllers.keys()).filter(k => k.startsWith(sessionId));
    keysToAbort.forEach(key => {
      const controller = this.abortControllers.get(key);
      if (controller) {
        controller.abort();
        this.abortControllers.delete(key);
      }
    });
  }

  async getSession(sessionId: string): Promise<SessionResponse> {
    const response = await this.client.get<SessionResponse>(`/sessions/${sessionId}`);
    return response.data;
  }

  async deleteSession(sessionId: string): Promise<void> {
    await this.client.delete(`/sessions/${sessionId}`);
  }

  async getTools(): Promise<ToolDefinition[]> {
    const response = await this.client.get<ToolDefinition[]>('/tools');
    return response.data;
  }

  async healthCheck(): Promise<boolean> {
    try {
      await this.client.get('/health');
      return true;
    } catch {
      return false;
    }
  }
}

export const apiClient = new ApiClient();
export default apiClient;