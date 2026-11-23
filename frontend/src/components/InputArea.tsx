import React, { useRef, useImperativeHandle, forwardRef, useState, useCallback } from 'react';

interface InputAreaProps {
  onSend: (message: string) => void;
  disabled?: boolean;
  placeholder?: string;
  streaming?: boolean;
  onAbort?: () => void;
}

interface InputAreaHandle {
  focus: () => void;
  clear: () => void;
}

export const InputArea = forwardRef<InputAreaHandle, InputAreaProps>(
  ({ onSend, disabled = false, placeholder = 'Digite sua mensagem...', streaming = false, onAbort }, ref) => {
    const textareaRef = useRef<HTMLTextAreaElement>(null);
    const [height, setHeight] = useState(48);
    const [showSend, setShowSend] = useState(false);

    useImperativeHandle(ref, () => ({
      focus: () => textareaRef.current?.focus(),
      clear: () => {
        if (textareaRef.current) {
          textareaRef.current.value = '';
          setHeight(48);
          textareaRef.current.style.height = '48px';
        }
      },
    }));

    const adjustHeight = useCallback(() => {
      const textarea = textareaRef.current;
      if (!textarea) return;
      
      textarea.style.height = 'auto';
      const newHeight = Math.min(textarea.scrollHeight, 180);
      setHeight(newHeight);
      textarea.style.height = `${newHeight}px`;
    }, []);

    const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      const value = e.target.value;
      setShowSend(value.trim().length > 0);
      setTimeout(adjustHeight, 0);
    };

    const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        const value = e.currentTarget.value.trim();
        if (value && !disabled && !streaming) {
          onSend(value);
          e.currentTarget.value = '';
          setShowSend(false);
          setHeight(48);
          e.currentTarget.style.height = '48px';
        }
      }
    };

    const handleSendClick = () => {
      const textarea = textareaRef.current;
      if (!textarea) return;
      
      const value = textarea.value.trim();
      if (value && !disabled && !streaming) {
        onSend(value);
        textarea.value = '';
        setShowSend(false);
        setHeight(48);
        textarea.style.height = '48px';
      }
    };

    return (
      <div className="relative bg-white border-t border-gray-200 p-4">
        <div className="flex items-end gap-3">
          <div className="flex-1 relative">
            <textarea
              ref={textareaRef}
              rows={1}
              placeholder={placeholder}
              disabled={disabled || streaming}
              onChange={handleChange}
              onKeyDown={handleKeyDown}
              className={`
                w-full px-4 py-3 pr-12
                bg-gray-50 border border-gray-200 rounded-xl
                text-gray-900 placeholder-gray-400
                focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent
                disabled:bg-gray-100 disabled:cursor-not-allowed disabled:text-gray-500
                resize-none transition-all duration-150
                min-h-[48px] max-h-[180px]
              `}
              style={{ height: `${height}px` }}
              aria-label="Mensagem"
              aria-describedby="input-hint"
            />
            <div id="input-hint" className="sr-only">
              Pressione Enter para enviar, Shift+Enter para nova linha
            </div>
          </div>

          <div className="flex items-center gap-2">
            {streaming && onAbort ? (
              <button
                type="button"
                onClick={onAbort}
                className={`
                  p-3 rounded-xl transition-all duration-150
                  bg-red-50 text-red-600 hover:bg-red-100
                  focus:outline-none focus:ring-2 focus:ring-red-500
                  flex items-center justify-center
                `}
                aria-label="Cancelar geração da resposta"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            ) : (
              <button
                type="button"
                onClick={handleSendClick}
                disabled={!showSend || disabled || streaming}
                className={`
                  p-3 rounded-xl transition-all duration-150
                  flex items-center justify-center
                  ${showSend && !disabled && !streaming
                    ? 'bg-blue-600 text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2'
                    : 'bg-gray-200 text-gray-400 cursor-not-allowed'
                  }
                `}
                aria-label={streaming ? 'Enviando...' : 'Enviar mensagem'}
                aria-disabled={!showSend || disabled || streaming}
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                </svg>
              </button>
            )}
          </div>
        </div>

        <div className="mt-2 flex items-center justify-between">
          <p className="text-xs text-gray-400">
            Pressione <kbd className="px-1.5 py-0.5 bg-gray-100 border border-gray-200 rounded text-gray-600 font-mono">Enter</kbd> para enviar, 
            <kbd className="px-1.5 py-0.5 bg-gray-100 border border-gray-200 rounded text-gray-600 font-mono">Shift+Enter</kbd> para nova linha
          </p>
          
          {streaming && (
            <div className="flex items-center gap-1.5 text-xs text-blue-600" role="status" aria-live="polite">
              <span className="relative flex h-2 w-2">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-blue-400 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2 w-2 bg-blue-500"></span>
              </span>
              <span>Gerando resposta...</span>
            </div>
          )}
        </div>
      </div>
    );
  }
);

InputArea.displayName = 'InputArea';

export default InputArea;