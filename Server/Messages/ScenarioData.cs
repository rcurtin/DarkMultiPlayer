using System;
using System.IO;
using MessageStream2;
using DarkMultiPlayerCommon;

namespace DarkMultiPlayerServer.Messages
{
    public class ScenarioData
    {
        public static void SendScenarioModules(ClientObject client, bool sendToAll)
        {
            int numberOfScenarioModules = Directory.GetFiles(Path.Combine(Server.universeDirectory, "Scenarios", client.playerName)).Length;
            int currentScenarioModule = 0;
            string[] scenarioNames = new string[numberOfScenarioModules];
            byte[][] scenarioDataArray = new byte[numberOfScenarioModules][];
            foreach (string file in Directory.GetFiles(Path.Combine(Server.universeDirectory, "Scenarios", client.playerName)))
            {
                //Remove the .txt part for the name
                scenarioNames[currentScenarioModule] = Path.GetFileNameWithoutExtension(file);
                scenarioDataArray[currentScenarioModule] = File.ReadAllBytes(file);
                currentScenarioModule++;
            }
            ServerMessage compressedMessage = new ServerMessage();
            ServerMessage newMessage = new ServerMessage();
            compressedMessage.type = ServerMessageType.SCENARIO_DATA;
            newMessage.type = ServerMessageType.SCENARIO_DATA;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(scenarioNames);
                foreach (byte[] scenarioData in scenarioDataArray)
                {
                    if (client.compressionEnabled)
                    {
                        mw.Write<byte[]>(Compression.CompressIfNeeded(scenarioData));
                    }
                    else
                    {
                        mw.Write<byte[]>(Compression.AddCompressionHeader(scenarioData, false));
                    }
                }
                compressedMessage.data = mw.GetMessageBytes();
            }

            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(scenarioNames);
                foreach (byte[] scenarioData in scenarioDataArray)
                {
                    mw.Write<byte[]>(Compression.AddCompressionHeader(scenarioData, false));
                }
                newMessage.data = mw.GetMessageBytes();
            }

            if (sendToAll)
            {
                ClientHandler.SendToAllAutoCompressed(client, compressedMessage, newMessage, true);
            }
            else
            {
                ClientHandler.SendToClient(client, compressedMessage, true);
            }
        }

        public static void HandleScenarioModuleData(ClientObject client, byte[] messageData)
        {
            using (MessageReader mr = new MessageReader(messageData))
            {
                //Don't care about subspace / send time.
                string[] scenarioName = mr.Read<string[]>();
                DarkLog.Debug("Saving " + scenarioName.Length + " scenario modules from " + client.playerName);

                for (int i = 0; i < scenarioName.Length; i++)
                {
                    byte[] scenarioData = Compression.DecompressIfNeeded(mr.Read<byte[]>());
                    File.WriteAllBytes(Path.Combine(Server.universeDirectory, "Scenarios", client.playerName, scenarioName[i] + ".txt"), scenarioData);
                }
            }
        }
    }
}

