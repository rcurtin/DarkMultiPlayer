using System;
using System.Collections.Generic;
using MessageStream2;
using DarkMultiPlayerCommon;

namespace DarkMultiPlayerServer.Messages
{
    public class Group
    {
        public static void SendAllGroupsToAllClients()
        {
            Dictionary<string, GroupObject> groupState = GroupSystem.fetch.GetCopy();
            foreach (KeyValuePair<string, GroupObject> kvp in groupState)
            {
                ServerMessage newMessage = new ServerMessage();
                newMessage.type = ServerMessageType.GROUP_SYSTEM;
                newMessage.data = GetGroupBytes(kvp.Key, kvp.Value);
                ClientHandler.SendToAll(null, newMessage, true);
            }
        }
        public static void SendAllGroupsToClient(ClientObject client)
        {
            Dictionary<string, GroupObject> groupState = GroupSystem.fetch.GetCopy();
            foreach (KeyValuePair<string, GroupObject> kvp in groupState)
            {
                ServerMessage newMessage = new ServerMessage();
                newMessage.type = ServerMessageType.GROUP_SYSTEM;
                newMessage.data = GetGroupBytes(kvp.Key, kvp.Value);
                ClientHandler.SendToClient(client, newMessage, true);
            }
        }

        public static void SendGroupToAll(string groupName, GroupObject group)
        {
            ServerMessage newMessage = new ServerMessage();
            newMessage.type = ServerMessageType.GROUP_SYSTEM;
            newMessage.data = GetGroupBytes(groupName, group);
            ClientHandler.SendToAll(null, newMessage, true);
        }

        private static byte[] GetGroupBytes(string groupName, GroupObject group)
        {
            byte[] returnBytes;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<int>((int)GroupMessageType.SET);
                mw.Write<string>(groupName);
                mw.Write<int>((int)group.privacy);
                if (group.privacy == GroupPrivacy.PRIVATE)
                {
                    bool passwordSet = (group.passwordHash != null);
                    mw.Write<bool>(passwordSet);
                    if (passwordSet)
                    {
                        //Send the salt so the user can send the correct hash back
                        mw.Write<string>(group.passwordSalt);
                    }
                }
                mw.Write<string[]>(group.members.ToArray());
                returnBytes = mw.GetMessageBytes();
            }
            return returnBytes;
        }

        public static void RemoveGroup(string groupName)
        {
            ServerMessage newMessage = new ServerMessage();
            newMessage.type = ServerMessageType.GROUP_SYSTEM;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<int>((int)GroupMessageType.REMOVE);
                mw.Write<string>(groupName);
            }
            ClientHandler.SendToAll(null, newMessage, true);
        }
    }
}

