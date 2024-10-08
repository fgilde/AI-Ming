using System.IO;
using Microsoft.ML.OnnxRuntime;

namespace Aimmy2.AILogic.Contracts;

public enum OnnxExecutionProvider
{
    Cpu,
    DirectML,
    Cuda,
    TensorRT
}

internal static class OnnxHelper
{
    private static void SetExecutionProviderFiles(OnnxExecutionProvider provider)
    {
        string sourceDirectory = string.Empty;
        string targetDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Set the source directory based on the selected provider
        switch (provider)
        {
            case OnnxExecutionProvider.DirectML:
                sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnxruntimes", "DirectML");
                break;
            case OnnxExecutionProvider.Cuda:
                sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnxruntimes", "Cuda");
                break;
            case OnnxExecutionProvider.TensorRT:
                sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnxruntimes", "Cuda");
                break;
            default:
                // CPU does not require additional native DLLs, so nothing to copy
                return;
        }

        // Copy the DLL files from the source directory to the target directory (overwrite existing files)
        CopyFiles(sourceDirectory, targetDirectory);
    }

    private static void CopyFiles(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Get all files in the source directory
        string[] files = Directory.GetFiles(sourceDir);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(targetDir, fileName);

            // Copy and overwrite the files
            File.Copy(file, destFile, true);
        }
    }

    public static OnnxExecutionProvider SetExecutionProvider(this SessionOptions sessionOptions, OnnxExecutionProvider preferredProvider)
    {
        //var sessionOptions = CreateDefaultSessionOptions();
        OnnxExecutionProvider[] fallbackOrder;

        switch (preferredProvider)
        {
            case OnnxExecutionProvider.TensorRT:
                fallbackOrder = new[] { OnnxExecutionProvider.TensorRT, OnnxExecutionProvider.Cuda, OnnxExecutionProvider.DirectML, OnnxExecutionProvider.Cpu };
                break;
            case OnnxExecutionProvider.Cuda:
                fallbackOrder = new[] { OnnxExecutionProvider.Cuda, OnnxExecutionProvider.DirectML, OnnxExecutionProvider.Cpu };
                break;
            case OnnxExecutionProvider.DirectML:
                fallbackOrder = new[] { OnnxExecutionProvider.DirectML, OnnxExecutionProvider.Cpu };
                break;
            default:
                fallbackOrder = new[] { OnnxExecutionProvider.Cpu };
                break;
        }

        foreach (var provider in fallbackOrder)
        {
            try
            {
                if (CanWork(provider))
                {
                    sessionOptions.AppendExecutionProvider(provider);
                    Console.WriteLine($"Initialized with provider {provider}");
                    return provider;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Failed to initialize provider {provider}. Message {e.Message}");
            }
        }

        throw new InvalidOperationException("No suitable execution provider found.");
    }

    public static SessionOptions AppendExecutionProvider(this SessionOptions options, OnnxExecutionProvider provider)
    {
        switch (provider)
        {
            case OnnxExecutionProvider.Cpu:
                options.AppendExecutionProvider_CPU();
                break;
            case OnnxExecutionProvider.DirectML:
                options.AppendExecutionProvider_DML();
                break;
            case OnnxExecutionProvider.Cuda:
                options.AppendExecutionProvider_CUDA();
                break;
            case OnnxExecutionProvider.TensorRT:
                options.AppendExecutionProvider_Tensorrt();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }
        return options;
    }

    public static SessionOptions CreateDefaultSessionOptions()
    {
        return new SessionOptions
        {
            EnableCpuMemArena = true,
            EnableMemoryPattern = true,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_PARALLEL
        };
    }

    public static bool CanWork(OnnxExecutionProvider provider)
    {
        try
        {
            CreateDefaultSessionOptions()
                .AppendExecutionProvider(provider);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to initialize provider {provider}. Message {e.Message}");
            return false;
        }
    }
}