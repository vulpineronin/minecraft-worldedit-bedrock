﻿using System;
using System.Collections.Generic;
using System.Threading;
using MinecraftPluginServer;
using WorldEdit.Input;
using WorldEdit.Output;

namespace WorldEdit
{
    public class PluginServer
    {
        private static bool keepRunning = true;
        private IMinecraftCommandService minecraftService;
        private List<IGameEventHander> GameHandlers { get; } = new List<IGameEventHander>();
        private List<IHotkeyHandler> HotkeyHandlers { get; } = new List<IHotkeyHandler>();

        public void Start()
        {
            using (var server = new SocketServer("ws://0.0.0.0:12112")) // will stop on disposal.
            {
                server.Start();
                minecraftService = new MinecraftWebsocketCommandService(server);
                var cmdHandler = new CommandControl(minecraftService, new WebsocketCommandFormater());
                server.AddHandler(new WorldEditHandler(cmdHandler));

                foreach (var gameHandler in GameHandlers)
                {
                    if (gameHandler is ISendCommand)
                    {
                        ((ISendCommand) gameHandler).CommandService = minecraftService;
                    }
                    server.AddHandler(gameHandler);
                }

                server.AddHandler(new ConnectionHandler(minecraftService));
                var ahk = AutoHotKey.Run("hotkeys.ahk");
                AutoHotKey.Callback = s =>
                {
                    Console.WriteLine(s);
                    var args = s.Split(' ');
                    HandleHotKeys(args);
                };

                using (var cancelationToken = minecraftService.Run())
                {
                    while (keepRunning)
                        Thread.Sleep(500);
                    ahk.Terminate();
                    minecraftService.Status("WorldEdit Shutting Down");
                    minecraftService.Wait();
                    minecraftService.ShutDown();
                    cancelationToken.Cancel();
                }
                server.Stop();
            }
        }

        public void Plugin(IGameEventHander handler)
        {
            GameHandlers.Add(handler);
        }

        public void Plugin(IHotkeyHandler handler)
        {
            HotkeyHandlers.Add(handler);
        }

        private void HandleHotKeys(string[] args)
        {
            foreach (var handler in HotkeyHandlers)
            {
                if (handler.CanHandle(args))
                {
                    handler.Handle(args, minecraftService);
                }
            }
        }

        public void Stop()
        {
            keepRunning = false;
        }
    }
}