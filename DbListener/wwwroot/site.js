window.initializeEditor = function () {
    // Проверяем, не загружен ли Monaco Editor уже
    if (typeof monaco !== 'undefined') {
        initMonacoEditor();
        return;
    }

    // Настройка пути для загрузки Monaco Editor
    const loaderScript = document.createElement('script');
    loaderScript.src = 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.33.0/min/vs/loader.min.js';
    loaderScript.onload = function () {
        require.config({
            paths: {
                'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.33.0/min/vs',
                'vs/basic-languages': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.33.0/min/vs/basic-languages',
                'vs/language': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.33.0/min/vs/language'
            }
        });

        require(['vs/editor/editor.main'], function () {
            if (typeof monaco === 'undefined') {
                console.error('Monaco Editor failed to load');
                return;
            }
            initMonacoEditor();
        });
    };
    loaderScript.onerror = function () {
        console.error('Failed to load Monaco Editor loader script');
    };
    document.head.appendChild(loaderScript);

    function initMonacoEditor() {
        try {
            // Определение языка
            monaco.languages.register({ id: 'myLanguage' });

            // Регистрация токенов (подсветка синтаксиса)
            monaco.languages.setMonarchTokensProvider('myLanguage', {
                tokenizer: {
                    root: [
                        [/\b(if|else|while|for|return|int|float|string)\b/, "keyword"],
                        [/\bГР\[(\d{8})]/, "custom-keyword"],
                        [/\bГР\[/, "custom-keyword"],
                        [/\bГР(?!\[)/, "error"],
                        [/\b[A-Za-zА-Яа-я_]\w*\b/, "identifier"],
                        [/\d+/, "number"],
                        [/"[^"]*"/, "string"],
                        [/\/\/.*$/, "comment"],
                    ],
                },
            });

            // Определение предложений для автодополнения
            const suggestions = [
                {
                    label: 'if',
                    kind: monaco.languages.CompletionItemKind.Keyword,
                    insertText: 'if ($1) {\n    $0\n}',
                    documentation: 'Conditional statement'
                },
                {
                    label: 'while',
                    kind: monaco.languages.CompletionItemKind.Keyword,
                    insertText: 'while ($1) {\n    $0\n}',
                    documentation: 'Loop statement'
                },
                {
                    label: 'for',
                    kind: monaco.languages.CompletionItemKind.Keyword,
                    insertText: 'for (let $1 = 0; $1 < $2; $1++) {\n    $0\n}',
                    documentation: 'Loop statement'
                },
                {
                    label: 'return',
                    kind: monaco.languages.CompletionItemKind.Keyword,
                    insertText: 'return $0;',
                    documentation: 'Return statement'
                },
                {
                    label: 'ГР',
                    kind: monaco.languages.CompletionItemKind.Keyword,
                    insertText: 'ГР[$1]',
                    documentation: 'Custom keyword'
                },
            ];

            // Регистрация провайдера автодополнения
            monaco.languages.registerCompletionItemProvider('myLanguage', {
                provideCompletionItems: function () {
                    return { suggestions: suggestions };
                }
            });

            // Функция для получения диагностических сообщений
            function getDiagnostics(model) {
                const diagnostics = [];
                const lines = model.getValue().split('\n');
                const regex = /ГР\[(\d*)]/g;

                lines.forEach((line, lineIndex) => {
                    let match;
                    while ((match = regex.exec(line)) !== null) {
                        const [fullMatch, number] = match;
                        if (number.length === 0 || number.length !== 8) {
                            const startColumn = match.index + 1;
                            const endColumn = startColumn + fullMatch.length;
                            diagnostics.push({
                                startLineNumber: lineIndex + 1,
                                startColumn: startColumn,
                                endLineNumber: lineIndex + 1,
                                endColumn: endColumn,
                                message: 'Ожидается 8-значное число внутри скобок [] после ГР',
                                severity: monaco.MarkerSeverity.Error
                            });
                        }
                    }
                });

                return diagnostics;
            }

            // Настройка редактора
            const editorOptions = {
                language: 'myLanguage',
                glyphMargin: true,
                automaticLayout: true,
                minimap: { enabled: true },
                value: [
                    'int main() {',
                    '    int x = 10;',
                    '    return x;',
                    '    ГР[12345678];', // Пример корректного использования ГР
                    '    ГР[123]; // Ошибка - не 8 цифр',
                    '}'
                ].join('\n')
            };

            // Проверяем существование элемента перед созданием редактора
            const editorElement = document.getElementById('editor');
            if (!editorElement) {
                console.error('Editor container element not found');
                return;
            }


            // Инициализация редактора
            const editor = monaco.editor.create(editorElement, editorOptions);

            // Установка диагностических сообщений
            const model = editor.getModel();
            monaco.editor.setModelMarkers(model, 'myLanguage', getDiagnostics(model));

            // Обновление диагностики при изменении модели
            model.onDidChangeContent(() => {
                monaco.editor.setModelMarkers(model, 'myLanguage', getDiagnostics(model));
            });

        } catch (error) {
            console.error('Error initializing Monaco Editor:', error);
        }
    }
};