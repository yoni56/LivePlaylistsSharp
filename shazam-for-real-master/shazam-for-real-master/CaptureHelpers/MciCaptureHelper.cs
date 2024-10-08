﻿using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Project;

public partial class MciCaptureHelper : ICaptureHelper {
    static readonly object SYNC = new();
    static readonly int GENERATION_COUNT = 3;
    static readonly TimeSpan GENERATION_STEP = TimeSpan.FromSeconds(4);
    static readonly string TEMP_FILE_PATH = Path.Combine(Path.GetTempPath(), "shazam-for-real-tmp.wav");

    readonly bool[] GenerationRecording = new bool[GENERATION_COUNT];
    readonly IList<Stream> GenerationStreams = new List<Stream>();

    DateTime StartTime;
    Thread WorkerThread;
    bool StopRequested;

    public MciCaptureHelper() {
        SampleProvider = new RawSourceWaveStream(Stream.Null, ICaptureHelper.WAVE_FORMAT).ToSampleProvider();
    }

    public void Dispose() {
        lock(SYNC) {
            StopRequested = true;
        }

        WorkerThread.Join();

        foreach(var s in GenerationStreams)
            s.Dispose();

        if(File.Exists(TEMP_FILE_PATH))
            File.Delete(TEMP_FILE_PATH);
    }

    public bool Live => true;
    public ISampleProvider SampleProvider { get; private set; }
    public Exception Exception { get; private set; }

    public void Start() {
        WorkerThread = new Thread(WorkerThreadProc_Guarded);
        WorkerThread.Start();
    }

    void WorkerThreadProc_Guarded() {
        try {
            WorkerThreadProc();
        } catch(Exception x) {
            Exception = x;
        }
    }

    void WorkerThreadProc() {
        lock(SYNC) {
            for(var i = 0; i < GENERATION_COUNT; i++) {
                var alias = GetAlias(i);
                var format = ICaptureHelper.WAVE_FORMAT;
                MciSend("open new Type waveaudio Alias", alias);
                MciSend("set", alias,
                    "bitspersample", format.BitsPerSample,
                    "channels", format.Channels,
                    "samplespersec", format.SampleRate,
                    "bytespersec", format.AverageBytesPerSecond,
                    "alignment", format.BlockAlign
                );
            }

            for(var i = 0; i < GENERATION_COUNT; i++) {
                MciSend("record", GetAlias(i));
                GenerationRecording[i] = true;
            }

            StartTime = DateTime.Now;
        }

        while(true) {
            lock(SYNC) {
                var allGenerationsStopped = true;

                for(var i = 0; i < GENERATION_COUNT; i++) {
                    if(GenerationRecording[i]) {
                        var willStop = StopRequested || DateTime.Now - StartTime > (1 + i) * GENERATION_STEP;

                        if(willStop) {
                            var alias = GetAlias(i);

                            if(!StopRequested) {
                                MciSend("save", alias, TEMP_FILE_PATH);
                                TempFileToSampleProvider();
                            }

                            MciSend("close", alias);
                            GenerationRecording[i] = false;
                        }
                    }

                    allGenerationsStopped = allGenerationsStopped && !GenerationRecording[i];
                }

                if(allGenerationsStopped) {
                    SampleProvider = EternalSilence.AppendTo(SampleProvider);
                    return;
                }
            }

            Thread.Sleep(100);
        }
    }

    void TempFileToSampleProvider() {
        var stream = new MemoryStream(File.ReadAllBytes(TEMP_FILE_PATH));
        GenerationStreams.Add(stream);
        SampleProvider = new WaveFileReader(stream).ToSampleProvider();
    }

    static string GetAlias(int i) {
        return "rec" + i;
    }

    static string MciSend(params object[] command) {
        return MciSend(String.Join(" ", command));
    }

    static string MciSend(string command) {
        //Console.WriteLine(command);

        var buf = ArrayPool<char>.Shared.Rent(128);

        try {
            var code = mciSendString(command, buf, buf.Length, IntPtr.Zero);

            if(code != 0) {
                mciGetErrorString(code, buf, buf.Length);
                throw new Exception(BufToString(buf));
            }

            return BufToString(buf);
        } finally {
            ArrayPool<char>.Shared.Return(buf);
        }
    }

    static string BufToString(char[] buf) {
        var zIndex = Array.IndexOf(buf, '\0');

        if(zIndex > 0)
            return new String(buf, 0, zIndex);

        return new String(buf);
    }

    [LibraryImport("winmm", EntryPoint = "mciSendStringW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint mciSendString(string command, [Out] char[] returnBuf, int returnLen, IntPtr callbackHandle);

    [LibraryImport("winmm", EntryPoint = "mciGetErrorStringW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void mciGetErrorString(uint errorCode, [Out] char[] returnBuf, int returnLen);
}
