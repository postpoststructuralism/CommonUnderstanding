# Common Understanding

An ASP.NET MVC application powered by Semantic Kernel and Ollama that maps belief systems onto one another, identifying areas of overlap and divergence to establish non-zero-sum games and emphasize what binds us together.

## Overview

Common Understanding uses AI-powered analysis to:
- Analyze belief systems, philosophies, and worldviews
- Compare different belief systems to find common ground
- Identify areas of divergence with potential bridges
- Discover non-zero-sum opportunities for collaboration
- Generate practical suggestions for constructive dialogue

## Prerequisites

1. **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
2. **Ollama** - [Download here](https://ollama.ai/)
3. A compatible Ollama model (e.g., llama3.2)

## Setup Instructions

### 1. Install and Run Ollama

First, install Ollama from [ollama.ai](https://ollama.ai/). Then download a model:

```bash
ollama pull llama3.2
```

Make sure Ollama is running:
```bash
ollama serve
```

By default, Ollama runs on `http://localhost:11434`. If you use a different endpoint, update `appsettings.json`.

### 2. Configure the Application

The application is pre-configured to connect to Ollama. You can modify settings in `appsettings.json`:

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2"
  }
}
```

Available models you might want to try:
- `llama3.2` - Fast and efficient (default)
- `llama3.1` - More powerful, slower
- `mistral` - Good alternative
- `phi3` - Lightweight option

### 3. Run the Application

Navigate to the project directory and run:

```bash
cd CommonUnderstanding
dotnet run
```

The application will start on `https://localhost:5001` (or the port shown in your terminal).

## Features

### 1. Create Belief Systems
Add belief systems by providing a name and description. The AI will analyze the content to extract:
- Core beliefs and tenets
- Fundamental values
- Guiding principles

### 2. Compare Belief Systems
Select two belief systems to compare. The AI will generate a comprehensive analysis including:
- **Areas of Overlap** - Shared values, principles, and goals
- **Areas of Divergence** - Key differences and potential bridges
- **Non-Zero-Sum Opportunities** - Ways both can benefit from collaboration
- **Synthesis Summary** - What fundamentally unites these perspectives

### 3. Dialogue Suggestions
Generate practical suggestions for constructive dialogue between adherents of different belief systems, including:
- Conversation starters emphasizing common ground
- Questions promoting mutual understanding
- Collaborative projects and activities
- Frameworks for discussing differences respectfully

## Project Structure

```
CommonUnderstanding/
├── Controllers/
│   └── BeliefSystemsController.cs    # Main MVC controller
├── Models/
│   ├── BeliefSystem.cs                # Belief system domain model
│   └── BeliefComparison.cs            # Comparison results model
├── Services/
│   ├── SemanticKernelService.cs       # Semantic Kernel configuration
│   └── BeliefAnalysisService.cs       # AI-powered analysis service
├── Views/
│   ├── BeliefSystems/
│   │   ├── Index.cshtml               # List all belief systems
│   │   ├── Create.cshtml              # Add new belief system
│   │   ├── Details.cshtml             # View belief system details
│   │   ├── Compare.cshtml             # Select systems to compare
│   │   ├── ComparisonResult.cshtml    # View comparison analysis
│   │   ├── Comparisons.cshtml         # List all comparisons
│   │   └── DialogueSuggestions.cshtml # Dialogue tips
│   └── Shared/
│       └── _Layout.cshtml             # Main layout
├── appsettings.json                   # Configuration
└── Program.cs                         # Application startup
```

## Technology Stack

- **ASP.NET Core 9.0 MVC** - Web framework
- **Microsoft Semantic Kernel** - AI orchestration framework
- **Ollama** - Local LLM runtime
- **Bootstrap 5** - UI framework
- **Bootstrap Icons** - Icon library

## Examples of Belief Systems to Analyze

- **Philosophies**: Buddhism, Stoicism, Existentialism, Pragmatism
- **Religious Traditions**: Christianity, Islam, Judaism, Hinduism
- **Modern Worldviews**: Secular Humanism, Scientific Materialism, Deep Ecology
- **Political Philosophies**: Liberalism, Conservatism, Libertarianism, Socialism
- **Ethical Frameworks**: Utilitarianism, Virtue Ethics, Deontology

## Current Limitations

- **In-Memory Storage**: Data is stored in memory and will be lost when the app restarts. For production, implement database persistence.
- **Basic Parsing**: AI responses are currently stored as raw text. Future versions should parse structured data.
- **Single User**: No authentication or multi-user support yet.
- **No Export**: Results cannot be exported or shared externally yet.

## Future Enhancements

- [ ] Database persistence (Entity Framework Core)
- [ ] Structured data extraction from AI responses
- [ ] User authentication and personal libraries
- [ ] Export comparisons as PDF or markdown
- [ ] Visual mapping of belief systems
- [ ] Community sharing and collaboration features
- [ ] Multi-way comparisons (more than 2 systems)
- [ ] Historical tracking of how beliefs evolve

## Philosophy

This application is built on the premise that:
- Most conflicts arise from misunderstanding rather than fundamental incompatibility
- Finding common ground doesn't require abandoning differences
- Non-zero-sum solutions often exist when we look beyond binary thinking
- What binds us together is often stronger than what divides us
- Respectful dialogue is the path to mutual understanding

## Contributing

This is a demonstration project. Feel free to fork and extend it with:
- Better data models and persistence
- More sophisticated AI prompts and agents
- Visualization features
- Additional analysis dimensions
- Export and sharing capabilities

## License

This project is provided as-is for educational and demonstration purposes.

---

**Remember**: The goal is not to prove one belief system "right" or "wrong," but to build bridges of understanding and identify opportunities for collaboration and mutual flourishing.
