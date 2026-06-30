$body = @{
    model = "deepseek-v4-flash"
    input = "test"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:11434/v1/embeddings" -Method Post -ContentType "application/json" -Body $body
Write-Output ($response | ConvertTo-Json -Depth 5)