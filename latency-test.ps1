# Latency Testing Script for Pension Calculation Engine
Write-Host "=== Pension Calculation Engine - Latency Test ===" -ForegroundColor Cyan
Write-Host ""

$url = "http://localhost:8081/calculation-requests"
$testRequestPath = "C:\Temp\FBB\Hackaton2026\hackathon-team-mericko-legends\test-request.json"
$body = Get-Content $testRequestPath -Raw

# Test 1: Cold Start (First Request)
Write-Host "Test 1: Cold Start (First Request)" -ForegroundColor Yellow
$coldStart = Measure-Command {
    $response = Invoke-WebRequest -Uri $url -Method POST -Body $body -ContentType "application/json" -ErrorAction Stop
}
Write-Host "  Latency: $($coldStart.TotalMilliseconds) ms" -ForegroundColor Green
Write-Host "  Status: $($response.StatusCode)" -ForegroundColor Green
Write-Host ""

# Test 2: Warm Requests (10 sequential requests)
Write-Host "Test 2: Warm Requests (10 sequential requests)" -ForegroundColor Yellow
$warmLatencies = @()
for ($i = 1; $i -le 10; $i++) {
    $time = Measure-Command {
        $response = Invoke-WebRequest -Uri $url -Method POST -Body $body -ContentType "application/json" -ErrorAction Stop
    }
    $warmLatencies += $time.TotalMilliseconds
    Write-Host "  Request $i`: $($time.TotalMilliseconds) ms"
}
Write-Host ""

# Calculate statistics
$avgLatency = ($warmLatencies | Measure-Object -Average).Average
$minLatency = ($warmLatencies | Measure-Object -Minimum).Minimum
$maxLatency = ($warmLatencies | Measure-Object -Maximum).Maximum

Write-Host "=== Warm Request Statistics ===" -ForegroundColor Cyan
Write-Host "  Average Latency: $([math]::Round($avgLatency, 2)) ms" -ForegroundColor Green
Write-Host "  Min Latency: $([math]::Round($minLatency, 2)) ms" -ForegroundColor Green
Write-Host "  Max Latency: $([math]::Round($maxLatency, 2)) ms" -ForegroundColor Green
Write-Host ""

# Test 3: Concurrent Requests (5 parallel requests)
Write-Host "Test 3: Concurrent Requests (5 parallel requests)" -ForegroundColor Yellow
$jobs = @()
$concurrentStart = Get-Date
for ($i = 1; $i -le 5; $i++) {
    $jobs += Start-Job -ScriptBlock {
        param($url, $body)
        $time = Measure-Command {
            $response = Invoke-WebRequest -Uri $url -Method POST -Body $body -ContentType "application/json" -ErrorAction Stop
        }
        return $time.TotalMilliseconds
    } -ArgumentList $url, $body
}

$concurrentLatencies = $jobs | Wait-Job | Receive-Job
$concurrentEnd = Get-Date
$totalConcurrentTime = ($concurrentEnd - $concurrentStart).TotalMilliseconds
$jobs | Remove-Job

Write-Host "  Total Time: $([math]::Round($totalConcurrentTime, 2)) ms" -ForegroundColor Green
Write-Host "  Individual Request Latencies:" -ForegroundColor Green
$concurrentLatencies | ForEach-Object { Write-Host "    $([math]::Round($_, 2)) ms" }
$avgConcurrent = ($concurrentLatencies | Measure-Object -Average).Average
Write-Host "  Average Concurrent Latency: $([math]::Round($avgConcurrent, 2)) ms" -ForegroundColor Green
Write-Host ""

# Test 4: Throughput Test (50 requests)
Write-Host "Test 4: Throughput Test (50 requests)" -ForegroundColor Yellow
$throughputStart = Get-Date
$throughputLatencies = @()
for ($i = 1; $i -le 50; $i++) {
    $time = Measure-Command {
        $response = Invoke-WebRequest -Uri $url -Method POST -Body $body -ContentType "application/json" -ErrorAction Stop
    }
    $throughputLatencies += $time.TotalMilliseconds
}
$throughputEnd = Get-Date
$totalThroughputTime = ($throughputEnd - $throughputStart).TotalSeconds

$throughputAvg = ($throughputLatencies | Measure-Object -Average).Average
$throughputMin = ($throughputLatencies | Measure-Object -Minimum).Minimum
$throughputMax = ($throughputLatencies | Measure-Object -Maximum).Maximum
$requestsPerSecond = 50 / $totalThroughputTime

Write-Host "  Total Time: $([math]::Round($totalThroughputTime, 2)) seconds" -ForegroundColor Green
Write-Host "  Requests Per Second: $([math]::Round($requestsPerSecond, 2))" -ForegroundColor Green
Write-Host "  Average Latency: $([math]::Round($throughputAvg, 2)) ms" -ForegroundColor Green
Write-Host "  Min Latency: $([math]::Round($throughputMin, 2)) ms" -ForegroundColor Green
Write-Host "  Max Latency: $([math]::Round($throughputMax, 2)) ms" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "  Cold Start: $([math]::Round($coldStart.TotalMilliseconds, 2)) ms" -ForegroundColor White
Write-Host "  Warm Average: $([math]::Round($avgLatency, 2)) ms" -ForegroundColor White
Write-Host "  Concurrent Average: $([math]::Round($avgConcurrent, 2)) ms" -ForegroundColor White
Write-Host "  Throughput: $([math]::Round($requestsPerSecond, 2)) req/s" -ForegroundColor White
Write-Host "  P50 (Median): $([math]::Round(($throughputLatencies | Sort-Object)[25], 2)) ms" -ForegroundColor White
Write-Host "  P95: $([math]::Round(($throughputLatencies | Sort-Object)[47], 2)) ms" -ForegroundColor White
Write-Host "  P99: $([math]::Round(($throughputLatencies | Sort-Object)[49], 2)) ms" -ForegroundColor White
Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan

