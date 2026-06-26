export interface OllamaCloudModel {
  id: string;
  name: string;
  tags: string;
}

// Every id here has been individually verified against its ollama.com/library/<model>/tags
// page (not just the cloud search listing, which only shows a curated subset and made
// gemma3/deepseek-v3.1 look retired when they aren't). Previous list carried forward four
// ids that don't exist on Ollama Cloud at all: qwen3-coder:32b-cloud (only 480b-cloud is a
// real cloud tag), deepseek-r1:671b-cloud (this model has no cloud variant), llama3.3:70b-
// cloud (same - no cloud variant), and glm4.5:30b-cloud (wrong format and GLM 4.5 isn't in
// the current cloud catalog at all - confirmed live by a 404 "model not found" from a real
// user). Selecting any of those four 404s with no recovery; removed rather than guessed at
// a fix, since there's no real replacement id for some of them. Added gemma4:31b-cloud,
// which is already AppSettings.DefaultVisionModel and several agent profiles' model
// elsewhere in this app, but was missing from this dropdown.
export const OLLAMA_CLOUD_MODELS: OllamaCloudModel[] = [
  { id: "gpt-oss:120b-cloud",        name: "GPT OSS 120B",       tags: "reasoning · large" },
  { id: "gpt-oss:20b-cloud",         name: "GPT OSS 20B",        tags: "fast · reasoning" },
  { id: "gemma4:31b-cloud",          name: "Gemma 4 31B",        tags: "chat · vision · balanced" },
  { id: "gemma3:27b-cloud",          name: "Gemma 3 27B",        tags: "chat · vision · balanced" },
  { id: "gemma3:12b-cloud",          name: "Gemma 3 12B",        tags: "fast · vision · chat" },
  { id: "gemma3:4b-cloud",           name: "Gemma 3 4B",         tags: "very fast · vision" },
  { id: "qwen3-coder:480b-cloud",    name: "Qwen3 Coder 480B",   tags: "code · large" },
  { id: "deepseek-v3.1:671b-cloud",  name: "DeepSeek V3.1 671B", tags: "chat · large" },
  { id: "deepseek-v4-flash:cloud",   name: "DeepSeek V4 Flash",  tags: "fast · 1M context" },
  { id: "glm-5.2:cloud",             name: "GLM 5.2",            tags: "huge context · 976K" }
];

export function populateOllamaCloudModelSelect(select: HTMLSelectElement, currentModel: string): void {
  select.replaceChildren(...OLLAMA_CLOUD_MODELS.map(m => {
    const option = document.createElement("option");
    option.value = m.id;
    option.textContent = `${m.name} · ${m.tags}`;
    return option;
  }));
  select.value = OLLAMA_CLOUD_MODELS.some(m => m.id === currentModel)
    ? currentModel
    : OLLAMA_CLOUD_MODELS[0]?.id ?? "";
}
