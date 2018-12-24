// This file is used in the Azure Pipelines PowerShell scripts via:
// Add-Type -Path "azure-pipelines-ext.cs"

// Note that this file is restricted to C# 5.0

using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using System.Net;

public class EverestPS {

    public static string ToHMACSHA1(string key, string dataToSign) {
        using (HMACSHA1 hmac = new HMACSHA1(UTF8Encoding.UTF8.GetBytes(key))) {
            return Convert.ToBase64String(hmac.ComputeHash(UTF8Encoding.UTF8.GetBytes(dataToSign)));
        }
    }

    public static void PutS3(string path, string file, string awsPath, string contentType) {
        const string bucket = "lollyde";
        const string aclKey = "x-amz-acl";
        const string aclValue = "public-read";

        string pathFull = Path.Combine(path, file);
        string date = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
        using (FileStream streamFile = File.OpenRead(pathFull)) {
            string signature = ToHMACSHA1(Environment.GetEnvironmentVariable("S3SECRET"), "PUT\n\n"+contentType+"\n"+date+"\n"+aclKey+":"+aclValue+"\n/"+bucket+awsPath+file);

            HttpWebRequest request = (HttpWebRequest) WebRequest.Create("https://"+bucket+".ams3.digitaloceanspaces.com/"+awsPath+file);
            request.Method = "PUT";
            request.Host = bucket+".ams3.digitaloceanspaces.com";
            // request.Date = date; // Expects DateTime
            request.Headers.Add("Date", date);
            request.ContentType = contentType;
            request.Headers.Add(aclKey, aclValue);
            request.Headers.Add("Authorization", "AWS "+Environment.GetEnvironmentVariable("S3KEY")+signature);
            request.ContentLength = streamFile.Length;

            using (Stream streamS3 = request.GetRequestStream()) {
                streamFile.CopyTo(streamS3);
            }
        }
    }

    public static void RebuildHTML(string pathBuilds, string pathHTML) {
        if (File.Exists(pathHTML))
            File.Delete(pathHTML);

        using (StreamWriter writer = new StreamWriter(pathHTML)) {
            writer.Write(
@"<!DOCTYPE html>
<html>
    <head>
        <title>Everest autobuild archives</title>

        <link rel=""stylesheet"" href=""https://overpass-30e2.kxcdn.com/overpass.css"">
        <style>
            html {
                min-height: 100vh;
            }
            body {
                font-family: 'overpass', sans-serif;
                background: #111111;
                color: rgba(255, 255, 255, 0.87);
                width: 100vw;
                overflow-x: hidden;
                margin: 0;
                padding: 0;
                line-height: 1.5em;
                font-weight: 300;
            }

            #main-wrapper {
                display: block;
                position: relative;
                margin-bottom: 10vh;
            }
            #main {
                display: block;
                position: relative;
                z-index: 0;
                min-height: calc(100vh - 256px + 64px);
                padding: 32px;
                word-wrap: break-word;
                opacity: 0;
                transform: translateX(-16px);
            }
            #main article.centered {
                max-width: calc(768px + 64px);
                margin: 0 auto 0 auto;
            }
            #main:not([data-fade]), #main[data-fade=""in""], .fadein {
                animation: main-in 0.15s both ease-out;
            }
            #main[data-fade=""out""], .fadeout {
                animation: main-out 0.15s both ease-in;
                pointer-events: none;
            }

            @keyframes main-in {
                0% {
                    opacity: 0;
                    transform: translateY(-16px) scale(0.99);
                }
                100% {
                    opacity: 1;
                    transform: translateY(0) scale(1);
                }
            }
            @keyframes main-out {
                0% {
                    opacity: 1;
                    transform: translateY(0) scale(1);
                }
                100% {
                    opacity: 0;
                    transform: translateY(16px) scale(0.99);
                }
            }

            h1, h2, h3, h4, h5, h6 {
                font-weight: 300;
                z-index: 0;
            }
            h1, h2, h3 {
                font-weight: 200;
            }
            h2 {
                margin-bottom: calc(1em + 16px);
            }
            h1 {
                font-size: 32px;
                line-height: 1.5em;
            }
            .sticky, .substicky {
                position: sticky;
                background: #111111;
                box-shadow: 0 0 16px 16px #111111;
                border-radius: 3px;
                padding-left: 32px;
                margin-left: -32px;
                padding-right: 64px;
            }
            .sticky {
                top: 48px;
                z-index: 500;
                width: 100%;
            }
            .substicky {
                top: calc(48px + 48px + 16px);
                padding-top: 16px;
                margin-top: calc(0.83em - 16px);
                z-index: 499;
            }
            .hinline {
                display: inline-block;
                line-height: inherit;
                margin: 0;
            }

            a, a:visited {
                transition: color 0.2s, text-decoration-color 0.2s, text-shadow 0.2s, border-bottom 0.2s;
                color: rgba(150, 230, 255, 0.87);
                text-decoration-color: rgba(150, 230, 255, 0.435);
                text-shadow: 0 0 0 rgba(150, 230, 255, 0);
                font-weight: 500;
                position: relative;
                display: inline-block;
            }
            a:not(.no-invert)::after {
                content: "";
                position: absolute;
                top: -8px;
                left: -8px;
                width: calc(100% + 16px);
                height: calc(100% + 16px);
                background: #ffffff;
                transition: transform 0.2s;
                transform-origin: 0% 100%;
                transform: scaleX(0);
                mix-blend-mode: difference;
                pointer-events: none;
            }
            a:not(.no-invert):hover::after {
                transform: scaleX(1);
            }
            a:hover, a:focus, a:active {
                color: rgba(150, 230, 255, 0.94);
                text-decoration-color: rgba(150, 230, 255, 0.94);
                text-shadow: 0 0 16px rgba(150, 230, 255, 0.2);
            }
        </style>
    </head>
    <body>
        <div id=""main-wrapper"">
            <div id=""main"">
                <article class=""centered"">
                    <h3>Everest autobuild archives</h3>
                    <p>Each .zip can be used to update or manually install Everest.</p>
                    <p><b>Note:</b> This service will possibly be replaced with the artifacts provided by <a href=""https://dev.azure.com/EverestAPI/Everest/_build?definitionId=1""Azure Pipelines</a> soon™.</p>
                    <ul>
");

            foreach (string line in File.ReadLines(pathBuilds, Encoding.UTF8)) {
                string[] split = line.Trim().Split(" ");
                writer.Write("<li><a href=\""+split[0]+"\">"+split[1]+"</a></li>");
            }

            writer.Write(
@"
                    </ul>
                </div>
            </div>
        </div>
    </body>
</html>
");


        }
    }

}
