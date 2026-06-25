export interface OllamaCloudModel {
  id: string;
  name: string;
  tags: string;
}

// Verified against ollama.com/library directly rather than carried forward as
// guesses — gpt-oss/qwen3-coder/deepseek-v3.1 tags confirmed via the cloud-models
// blog post, glm-5.2 and deepseek-v4-flash confirmed via their library pages. The
// rest came from the user's own list, not independently re-verified here, but
// failure mode for a wrong one is a clear "model not found" error (post-L2), not
// a silent break.
export const OLLAMA_CLOUD_MODELS: OllamaCloudModel[] = [
  { id: "gpt-oss:120b-cloud",        name: "GPT OSS 120B",       tags: "reasoning · large" },
  { id: "gpt-oss:20b-cloud",         name: "GPT OSS 20B",        tags: "fast · reasoning" },
  { id: "gemma3:27b-cloud",          name: "Gemma 3 27B",        tags: "chat · balanced" },
  { id: "gemma3:12b-cloud",          name: "Gemma 3 12B",        tags: "fast · chat" },
  { id: "gemma3:4b-cloud",           name: "Gemma 3 4B",         tags: "very fast" },
  { id: "qwen3-coder:480b-cloud",    name: "Qwen3 Coder 480B",   tags: "code · large" },
  { id: "qwen3-coder:32b-cloud",     name: "Qwen3 Coder 32B",    tags: "code · fast" },
  { id: "deepseek-r1:671b-cloud",    name: "DeepSeek R1 671B",   tags: "reasoning · largest" },
  { id: "deepseek-v3.1:671b-cloud",  name: "DeepSeek V3.1 671B", tags: "chat · large" },
  { id: "deepseek-v4-flash:cloud",   name: "DeepSeek V4 Flash",  tags: "fast · 1M context" },
  { id: "llama3.3:70b-cloud",        name: "Llama 3.3 70B",      tags: "chat · balanced" },
  { id: "glm4.5:30b-cloud",          name: "GLM 4.5 30B",        tags: "chat · multilingual" },
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
