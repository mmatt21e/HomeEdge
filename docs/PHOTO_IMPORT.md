# Photo → Inventory (AI vision)

HomeStock can look at a **photo** and suggest inventory items to add — either several items in a
scene (a shelf, drawer, or pile) or the details of a single product. You review and edit the
suggestions before anything is saved.

This feature is **optional and disabled by default**. When it's off, the *Add from photo* screen
simply explains that it isn't configured. Turn it on by pointing HomeStock at any
**OpenAI-compatible vision endpoint** — including several **free** options.

> **Privacy note:** the photo is sent from the HomeStock **server** to whichever provider you
> configure (your API key never reaches the browser). If you don't want images to leave your
> network, use the **local Ollama** option below.

## How it works

You set three things — a base URL, a model id, and (usually) an API key. HomeStock calls the
provider's `/chat/completions` endpoint with the image and parses the returned item list. Because
the interface is the widely-supported OpenAI chat format, many providers work unchanged.

## Free options

Model availability changes over time — check each provider's current **vision** model list. Set
these via environment variables (Docker `.env`) or `appsettings`.

### OpenRouter (free tier)
```
Vision__BaseUrl=https://openrouter.ai/api/v1
Vision__Model=meta-llama/llama-3.2-11b-vision-instruct:free
Vision__ApiKey=<your OpenRouter key>
```
OpenRouter also accepts optional attribution headers; add them if you like:
`Vision__ExtraHeaders__HTTP-Referer=http://homestock.local` and `Vision__ExtraHeaders__X-Title=HomeStock`.

### Groq (free tier)
```
Vision__BaseUrl=https://api.groq.com/openai/v1
Vision__Model=<a current Groq vision model>
Vision__ApiKey=<your Groq key>
```

### Google Gemini (free tier, OpenAI-compatible endpoint)
```
Vision__BaseUrl=https://generativelanguage.googleapis.com/v1beta/openai
Vision__Model=gemini-2.0-flash
Vision__ApiKey=<your Google AI Studio key>
```

### Local Ollama (fully private, no key, no cloud)
Run a vision model locally, then point HomeStock at it — images never leave your network:
```
Vision__BaseUrl=http://host.docker.internal:11434/v1   # or http://localhost:11434/v1 outside Docker
Vision__Model=llama3.2-vision
Vision__RequireApiKey=false
```
(Install once with `ollama pull llama3.2-vision`. Local accuracy is lower than the cloud models.)

## Paid / higher-accuracy options

Any OpenAI-compatible vision model works, e.g. OpenAI (`https://api.openai.com/v1`, a GPT vision
model) or an Anthropic-compatible gateway. Just set `BaseUrl`, `Model`, and `ApiKey`.

## All settings (`Vision` section)

| Key | Meaning |
|-----|---------|
| `Vision:BaseUrl` | OpenAI-compatible API base (…`/v1`). Blank = feature disabled. |
| `Vision:Model` | Vision model id. Blank = disabled. |
| `Vision:ApiKey` | Bearer key (omit for keyless local providers). |
| `Vision:RequireApiKey` | Set `false` for keyless providers (Ollama). Default `true`. |
| `Vision:ProviderName` | Friendly label shown in the UI (defaults to the host). |
| `Vision:MaxImageBytes` | Upload size cap (default 6 MB). |
| `Vision:TimeoutSeconds` | Request timeout (default 60). |
| `Vision:MaxItems` | Max items accepted from one photo (default 40). |
| `Vision:ExtraHeaders` | Extra request headers (e.g. OpenRouter attribution). |

## Using it

1. Go to **Items → Add from photo** (or the link on the **Scan** page). Requires an editor/admin role.
2. Choose **Multiple items (scene)** or **Single product**, then take/choose a photo.
3. Review the detected items — untick any you don't want, fix names/categories/quantities.
4. Optionally assign them all to a location and attach the photo to the created items.
5. **Create** — the items are added (and appear in the change history).

## Tips & limitations

- Clear, well-lit, close photos work best; busy scenes may miss or merge items.
- The model **won't invent** serial numbers or prices — add those yourself afterwards.
- Detected categories are matched to your existing categories by name; unmatched ones are left
  blank for you to set.
- This complements barcode scanning (Scan page) — use scanning for exact product identity.
