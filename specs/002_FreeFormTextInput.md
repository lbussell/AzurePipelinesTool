# AzurePipelinesTool Spec #002: Free-Form Text Input

In InteractionService, something like this:

```cs
Task<string> SelectAsync(string prompt, IEnumerable<string> suggestions);
```

Proposed usage:

```cs
var userInput = await _interactionService.SelectAsync(
    prompt: "Enter a tag for the pipeline:",
    suggestions: ["foo", "bar", "baz"]);
```

- Prompt the user for a selection. The selection should have the options:
    - Each of the suggested options.
    - An option "Something else..."
- If the user selects "Something else...", prompt them to enter free-form text.
- Return the selected option or the free-form text entered by the user.
