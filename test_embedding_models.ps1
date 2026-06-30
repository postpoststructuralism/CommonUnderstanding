$models = @("text-embedding-3-small", "text-embedding-ada-002", "nomic-embed-text", "all-minilm")

foreach ($model in $models) {
    try {
        $body = @{ model = $model; input = "test" } | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "http://localhost:11434/v1/embeddings" -Method Post -ContentType "application/json" -Body $body -ErrorAction Stop
        Write-Output "✅ $model works! Embedding dims: $($response.data[0].embedding.Count)"
    } catch {
        Write-Output "❌ $model failed: $($_.Exception.Message.Substring(0, [Math]::Min(100, $_.Exception.Message.Length)))"
    }
}