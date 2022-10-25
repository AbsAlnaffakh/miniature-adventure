using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ReliableDownloader
{
    public class FileDownloader : IFileDownloader
    {
        private readonly IWebSystemCalls _clientWrapper;
        private readonly CancellationTokenSource _tokenSource;
        private readonly CancellationToken _cancellationToken;
        
        public FileDownloader()
        {
            _clientWrapper = new WebSystemCalls();
            _tokenSource = new CancellationTokenSource();
            _cancellationToken = _tokenSource.Token;
        }

        public async Task<bool> DownloadFile(string contentFileUrl, string localFilePath, Action<FileProgress> onProgressChanged)
        {
            bool isSuccessful = false;

            try
            {
                // TODO - What if the call to fetch headers was made while the network is disconnected?
                var headerResponse = await _clientWrapper.GetHeadersAsync(contentFileUrl, _cancellationToken);

                // TODO - Do we want to penalise all users with partial downloads or only those that experience network issues?
                // partial downloads lead to slower downaloads as multiple calls will be made to fetch file contents rather than a single call
                var downloadedContent = ArePartialRequestsSupported(headerResponse) ?
                    await PartialDownload(contentFileUrl, headerResponse.Content.Headers.ContentLength.Value, onProgressChanged) :
                    await FullDownload(contentFileUrl, onProgressChanged);

                var isFileIntegrityOkay = IsContentIntegrityOk(downloadedContent, headerResponse.Content.Headers.ContentMD5);
                
                if (isFileIntegrityOkay)
                {
                    await File.WriteAllBytesAsync(localFilePath, downloadedContent, _cancellationToken);
                }                

                isSuccessful = isFileIntegrityOkay;
            }
            catch
            {
                // TODO - Don't just throw away the exception, at least log it and handle different exception types differently, avoid generic catch blocks
            }

            return isSuccessful;
        }

        public void CancelDownloads()
        {
            _tokenSource.Cancel();
        }

        protected virtual async Task<byte[]> PartialDownload(string contentFileUrl, long totalFileSize, Action<FileProgress> onProgressChanged)
        {
            // Download the file in intervals of 10% on each occassion
            const float intervalPrecentile = 0.10f;

            // TODO - Consider dynamically changing this based on how fast/slow good/bad the download goes
            // Would be nice if we can query the Ookla API to determine the speed of our connection
            // and set the intervals accordingly
            var downloadInterval = (long)Math.Round(intervalPrecentile * totalFileSize);
            var downloadedContent = new byte[totalFileSize];

            var fileDownloadProgress = new FileProgress(
                totalFileSize: totalFileSize,
                totalBytesDownloaded: 0,
                progressPercent: 0,
                estimatedRemaining: null);

            while (fileDownloadProgress.TotalBytesDownloaded < fileDownloadProgress.TotalFileSize && !_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var bytesToDownload = fileDownloadProgress.TotalBytesDownloaded + downloadInterval;

                    var partialContent = await _clientWrapper.DownloadPartialContent(
                        url: contentFileUrl,
                        from: fileDownloadProgress.TotalBytesDownloaded,
                        to: bytesToDownload,
                        token: _cancellationToken);

                    var bytes = await partialContent.Content.ReadAsByteArrayAsync();

                    bytes.CopyTo(downloadedContent, fileDownloadProgress.TotalBytesDownloaded);

                    fileDownloadProgress = GetUpdateFileProgress(fileDownloadProgress, bytesToDownload);

                    onProgressChanged(fileDownloadProgress);
                }
                catch (HttpRequestException e)
                {
                    // Potential network disconnection, swallow this up and loop again 
                }
            }

            return downloadedContent;
        }

        protected virtual async Task<byte[]> FullDownload(string contentFileUrl, Action<FileProgress> onProgressChanged)
        {
            // TODO - Consider reading as a stream and writing from that stream directly into file stream
            // to consume less resources rather than loading all bytes into memory

            var response = await _clientWrapper.DownloadContent(contentFileUrl, _cancellationToken);
            var downloadedContent = await response.Content.ReadAsByteArrayAsync();

            // Difficult to report progress if the conent is being downloaded in a single call
            // This may be possible if the connection speed is known

            return downloadedContent;
        }

        protected virtual FileProgress GetUpdateFileProgress(FileProgress progressToDate, long totalBytesDownloaded)
        {
            var precentileCompleted = Math.Round((double)((double)progressToDate.TotalBytesDownloaded / progressToDate.TotalFileSize * 100), 4);
            
            var updatedFileProgress = new FileProgress(
                totalFileSize: progressToDate.TotalFileSize, 
                totalBytesDownloaded: totalBytesDownloaded, 
                progressPercent: precentileCompleted, 
                estimatedRemaining: null);
            
            return updatedFileProgress;
        }

        protected virtual bool ArePartialRequestsSupported(HttpResponseMessage response)
        {
            const string partialContentHeader = "Bytes";
            var arePartialRequestsSupported = response.Headers.AcceptRanges.Contains(partialContentHeader);
            return arePartialRequestsSupported;
        }

        protected virtual bool IsContentIntegrityOk(byte[] downloadedContent, byte[]? contentMd5)
        {
            // TODO - Consider the cost of MD5CryptoServiceProvider, should this be a singleton?
            return contentMd5 == null || new MD5CryptoServiceProvider().ComputeHash(downloadedContent).SequenceEqual(contentMd5);
        }
    }
}