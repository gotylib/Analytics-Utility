window.initializeEditor = function () {
    require.config({ paths: { 'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.33.0/min/vs' } });
    require(['vs/editor/editor.main', 'vs/basic-languages/monarch/monarch', 'vs/editor/contrib/bracketMatching/bracketMatching', 'vs/language/json/languageFeatures'], function () {
        if (typeof monaco !== 'undefined' && monaco.languages && monaco.languages.register) {
            // Определение языка
            monaco.languages.register({ id: 'myLanguage' });

            // Регистрация токенов (подсветка синтаксиса)
            monaco.languages.setMonarchTokensProvider('myLanguage', {
                tokenizer: {
                    root: [
                        [/\b(if|else|while|for|return|int|float|string)\b/, "keyword"],
                        [/\bГР\[(\d{8})]/, "custom-keyword"], // Подсветка для ГР с 8-значным числом
                        [/\bГР\[/, "custom-keyword"], // Подсветка для ГР с открывающей скобкой
                        [/\bГР(?!\[)/, "error"], // Подсветка ошибки, если ГР без скобки
                        [/\b[A-Za-zА-Яа-я_]\w*\b/, "identifier"], // Поддержка русских идентификаторов
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
                            const startColumn = line.indexOf(fullMatch);
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
                value: [
                    'int main() {',
                    '    int x = 10;',
                    '    return x;',
                    '    ГР[12345678];', // Пример корректного использования ГР
                    '}'
                ].join('\n')
            };

            // Инициализация редактора
            const editor = monaco.editor.create(document.getElementById('editor'), editorOptions);

            // Установка диагностических сообщений
            const model = editor.getModel();
            const updateDiagnostics = monaco.editor.setModelMarkers(model, 'myLanguage', getDiagnostics(model));

            // Обновление диагностики при изменении модели
            model.onDidChangeContent(() => {
                updateDiagnostics(getDiagnostics(model));
            });
        } else {
            console.error('Monaco editor or its languages module failed to load.');
        }
    });
};
