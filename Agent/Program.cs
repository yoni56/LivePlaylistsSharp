﻿using Agent.Strategies;
using DotNetEnv;
using FluentScheduler;
using LivePlaylistsClone.Channels;
using LivePlaylistsClone.Playlists;
using System;
using System.Diagnostics;

Env.Load();

#if DEBUG
JobManager.Initialize(
    new Channel(
        new Kan88Channel(), 
        [
            new ShazamStrategy(),
            new AudDStrategy()
        ],
        [
            new SpotifyPlaylist("3zscpk0xnUckJs9BXoRwWX")
        ]
    )
);
#elif RELEASE
JobManager.Initialize(
    new Channel(
        new GlglzChannel(), 
        [
            new ShazamStrategy(),
            new AudDStrategy()
        ], 
        [
            new SpotifyPlaylist("5mLHWcR8C3ObKYdKxTyzyY")
        ]
    ),
    new Channel(
        new Kan88Channel(), 
        [
            new ShazamStrategy(),
            new AudDStrategy()
        ], 
        [
            new SpotifyPlaylist("3SpUq03whlfRMwEHMRulNy")
        ]
    )
);
#endif

// get current instance
var instance = Process.GetCurrentProcess();

// print pid
Console.WriteLine($"Agent Running... PID {instance.Id}");

// idle..
Console.ReadKey();
