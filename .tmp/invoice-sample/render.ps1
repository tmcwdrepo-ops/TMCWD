Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime] | Out-Null
function AwaitResult($operation, $type) {
  $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
  $task = $method.MakeGenericMethod($type).Invoke($null,@($operation))
  $task.Wait()
  $task.Result
}
$file = AwaitResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync('c:\scanning\Ulit\20261004_212534.pdf')) ([Windows.Storage.StorageFile])
$pdf = AwaitResult ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
Write-Output "Pages: $($pdf.PageCount)"
$page = $pdf.GetPage(0)
$stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
$action = $page.RenderToStreamAsync($stream)
$actionMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and -not $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 }; $actionTask = $actionMethod.Invoke($null,@($action)); $actionTask.Wait()
$stream.Seek(0)
$reader = New-Object Windows.Storage.Streams.DataReader($stream)
$loaded = AwaitResult ($reader.LoadAsync([uint32]$stream.Size)) ([uint32])
$bytes = New-Object byte[] $stream.Size
$reader.ReadBytes($bytes)
[System.IO.File]::WriteAllBytes('D:\Projects 20260720\TMCWD\.tmp\invoice-sample\sample.png',$bytes)
$page.Dispose()

