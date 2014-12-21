using System;
using System.IO;
using System.Collections.Generic;
using DarkMultiPlayerCommon;

namespace DarkMultiPlayerServer
{
    public class GroupSystem
    {
        private static GroupSystem singleton;
        //Data structure
        private Dictionary<string, GroupObject> groups;
        //Directories
        public string groupDirectory
        {
            private set;
            get;
        }

        public string playerDirectory
        {
            private set;
            get;
        }

        public GroupSystem()
        {
            groupDirectory = Path.Combine(Server.universeDirectory, "Groups");
            playerDirectory = Path.Combine(Server.universeDirectory, "Players");
            LoadGroups();
        }

        public static GroupSystem fetch
        {
            get
            {
                if (singleton == null)
                {
                    singleton = new GroupSystem();
                }
                return singleton;
            }
        }

        public Dictionary<string, GroupObject> GetCopy()
        {
            Dictionary<string, GroupObject> returnDictionary = new Dictionary<string, GroupObject>();
            lock (groups)
            {

                foreach (KeyValuePair<string, GroupObject> kvp in groups)
                {
                    GroupObject newGroupObject = new GroupObject();
                    newGroupObject.passwordSalt = kvp.Value.passwordSalt;
                    newGroupObject.passwordHash = kvp.Value.passwordHash;
                    newGroupObject.privacy = kvp.Value.privacy;
                    newGroupObject.members = new List<string>(kvp.Value.members);
                    returnDictionary.Add(kvp.Key, newGroupObject);
                }
            }
            return returnDictionary;
        }

        private void LoadGroups()
        {
            DarkLog.Debug("Loading groups");
            groups = new Dictionary<string, GroupObject>();
            string[] groupPaths = Directory.GetDirectories(groupDirectory);
            foreach (string groupPath in groupPaths)
            {
                string groupName = Path.GetFileName(groupPath);
                GroupObject newGroup = new GroupObject();
                string thisGroupDirectory = Path.Combine(groupDirectory, groupName);
                string membersFile = Path.Combine(thisGroupDirectory, "members.txt");
                string settingsFile = Path.Combine(thisGroupDirectory, "settings.txt");
                if (!File.Exists(membersFile))
                {
                    DarkLog.Error("Group " + groupName + " is broken (members file), skipping!");
                    continue;
                }
                if (!File.Exists(settingsFile))
                {
                    DarkLog.Error("Group " + groupName + " is broken (settings file), skipping!");
                    continue;
                }
                using (StreamReader sr = new StreamReader(membersFile))
                {
                    string currentLine = null;
                    while ((currentLine = sr.ReadLine()) != null)
                    {
                        newGroup.members.Add(currentLine);
                    }
                }
                int lineIndex = 0;
                using (StreamReader sr = new StreamReader(settingsFile))
                {
                    string currentLine = null;
                    while ((currentLine = sr.ReadLine()) != null)
                    {
                        switch (lineIndex)
                        {
                            case 0:
                                newGroup.privacy = (GroupPrivacy)Enum.Parse(typeof(GroupPrivacy), currentLine);
                                break;
                            case 1:
                                newGroup.passwordSalt = currentLine;
                                break;
                            case 2:
                                newGroup.passwordHash = currentLine;
                                break;
                        }
                        lineIndex++;
                    }
                }
                if (newGroup.members.Count > 0)
                {
                    groups.Add(groupName, newGroup);
                }
                else
                {
                    DarkLog.Error("Group " + groupName + " is broken (no members), skipping!");
                }
            }
        }

        private void SaveGroup(string groupName)
        {
            if (!GroupExists(groupName))
            {
                Console.WriteLine("Cannot save group " + groupName + ", doesn't exist");
                return;
            }
            DarkLog.Debug("Saving " + groupName);
            GroupObject saveGroup = groups[groupName];
            string thisGroupDirectory = Path.Combine(groupDirectory, groupName);
            if (!Directory.Exists(thisGroupDirectory))
            {
                Directory.CreateDirectory(thisGroupDirectory);
            }
            string membersFile = Path.Combine(thisGroupDirectory, "members.txt");
            string settingsFile = Path.Combine(thisGroupDirectory, "settings.txt");
            using (StreamWriter sw = new StreamWriter(membersFile + ".new"))
            {
                foreach (string member in saveGroup.members)
                {
                    sw.WriteLine(member);
                }
            }
            File.Copy(membersFile + ".new", membersFile, true);
            File.Delete(membersFile + ".new");
            using (StreamWriter sw = new StreamWriter(settingsFile + ".new"))
            {
                sw.WriteLine(saveGroup.privacy.ToString());
                if (saveGroup.passwordSalt != null)
                {
                    sw.WriteLine(saveGroup.passwordSalt);
                    if (saveGroup.passwordHash != null)
                    {
                        sw.WriteLine(saveGroup.passwordHash);
                    }
                }
            }
            File.Copy(settingsFile + ".new", settingsFile, true);
            File.Delete(settingsFile + ".new");
        }

        public bool GroupExists(string groupName)
        {
            lock (groups)
            {
                return groups.ContainsKey(groupName);
            }
        }

        public bool PlayerExists(string playerName)
        {
            return File.Exists(Path.Combine(playerDirectory, playerName + ".txt"));
        }

        public bool PlayerIsInGroup(string playerName)
        {
            return (GetPlayerGroup(playerName) != null);
        }

        public string GetPlayerGroup(string playerName)
        {
            string returnGroup = null;
            lock (groups)
            {
                foreach (KeyValuePair<string,GroupObject> kvp in groups)
                {
                    if (kvp.Value.members.Contains(playerName))
                    {
                        returnGroup = kvp.Key;
                        break;
                    }
                }
            }
            return returnGroup;
        }

        public string GetGroupOwner(string groupName)
        {
            if (!GroupExists(groupName))
            {
                return null;
            }
            return groups[groupName].members[0];
        }

        /// <summary>
        /// Creates the group. Returns true if successful.
        /// </summary>
        public bool CreateGroup(ClientObject callingClient, string groupName, string ownerName, GroupPrivacy groupPrivacy)
        {
            lock (groups)
            {
                if (GroupExists(groupName))
                {
                    string errorText = "Cannot create group " + groupName + ", Group already exists";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (PlayerIsInGroup(ownerName))
                {
                    string errorText = "Cannot create group " + groupName + ", " + ownerName + " already belongs to a group";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (!PlayerExists(ownerName))
                {
                    string errorText = "Cannot create group " + groupName + ", " + ownerName + " does not exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                GroupObject go = new GroupObject();
                go.members.Add(ownerName);
                go.privacy = groupPrivacy;
                groups.Add(groupName, go);
                DarkLog.Debug(ownerName + " created group " + groupName);
                Messages.Chat.SendChatMessageToClient(callingClient, "You created " + groupName);
                Messages.Group.SendGroupToAll(groupName, go);
                SaveGroup(groupName);
                return true;
            }
        }

        /// <summary>
        /// Make a player join the group. Returns true if the group was joined.
        /// </summary>
        public bool JoinGroup(ClientObject callingClient, string groupName, string playerName)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    string errorText = "Cannot join group " + groupName + ", Group does not exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (PlayerIsInGroup(playerName))
                {
                    string errorText = "Cannot join group " + groupName + ", " + playerName + " already belongs to a group";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (!PlayerExists(playerName))
                {
                    string errorText = "Cannot join group " + groupName + ", " + playerName + " doesn't exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                GroupObject go = groups[groupName];
                go.members.Add(playerName);
                DarkLog.Debug(playerName + " joined " + groupName);
                Messages.Chat.SendChatMessageToClient(callingClient, "You joined " + groupName);
                Messages.Group.SendGroupToAll(groupName, go);
                SaveGroup(groupName);
                return true;
            }
        }

        /// <summary>
        /// Make a player leave the group. Returns true if the group was left.
        /// </summary>
        public bool LeaveGroup(ClientObject callingClient, string playerName)
        {
            lock (groups)
            {
                if (!PlayerExists(playerName))
                {
                    string errorText = "Cannot leave group, " + playerName + " doesn't exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                string playerGroupName = GetPlayerGroup(playerName);
                if (playerGroupName == null)
                {
                    string errorText = "Cannot leave group, " + playerName + " does not belong to any a group";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (playerName == GetGroupOwner(playerGroupName))
                {
                    string errorText = "Cannot leave group, " + playerName + " is the owner";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                GroupObject go = groups[playerGroupName];
                go.members.Remove(playerName);
                DarkLog.Debug(playerName + " left " + playerGroupName);
                Messages.Chat.SendChatMessageToClient(callingClient, "You left " + playerGroupName);
                Messages.Group.SendGroupToAll(playerGroupName, go);
                SaveGroup(playerGroupName);
                return true;
            }
        }

        public bool RemoveGroup(ClientObject callingClient, string groupName)
        {
            lock (groups)
            {
                if (!groups.ContainsKey(groupName))
                {
                    string errorText = "Cannot remove group, " + groupName + " doesn't exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                GroupObject go = groups[groupName];
                string thisGroupDirectory = Path.Combine(groupDirectory, groupName);
                Directory.Delete(thisGroupDirectory, true);
                groups.Remove(groupName);
                DarkLog.Debug("Deleted group " + groupName);
                Messages.Chat.SendChatMessageToClient(callingClient, "You deleted " + groupName);
                Messages.Group.RemoveGroup(groupName);
                return true;
            }
        }

        /// <summary>
        /// Sets the group owner. If the group or player does not exist, or the player already belongs to a different group, this method returns false
        /// </summary>
        public bool SetGroupOwner(ClientObject callingClient, string groupName, string playerName)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    string errorText = "Cannot set group owner, " + groupName + " doesn't exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (!PlayerExists(playerName))
                {
                    string errorText = "Cannot set group owner, " + playerName + " does not exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                if (PlayerIsInGroup(playerName))
                {
                    if (GetPlayerGroup(playerName) != groupName)
                    {
                        string errorText = "Cannot set group owner, " + playerName + " already belongs to another group";
                        DarkLog.Debug(errorText);
                        Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                        return false;
                    }
                }
                else
                {
                    if (!JoinGroup(callingClient, groupName, playerName))
                    {
                        string errorText = "Cannot set group owner, " + playerName + " failed to join the group";
                        DarkLog.Debug(errorText);
                        Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                        return false;
                    }
                }
                GroupObject go = groups[groupName];
                go.members.Remove(playerName);
                go.members.Insert(0, playerName);
                DarkLog.Debug("Leader of group " + groupName + " changed to " + playerName);
                Messages.Chat.SendChatMessageToClient(callingClient, "You became leader of " + groupName);
                Messages.Group.SendGroupToAll(groupName, go);
                SaveGroup(groupName);
                return true;
            }
        }

        /// <summary>
        /// Sets the group password, with a raw, unencrypted password. Set to null or empty string to remove the password. Returns true on success.
        /// </summary>
        public bool SetGroupPasswordRaw(string groupName, string password)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    DarkLog.Debug("Cannot set group password, " + groupName + " doesn't exist");
                    return false;
                }
                GroupObject go = groups[groupName];
                if (password == null || password == "")
                {
                    go.passwordSalt = null;
                    go.passwordHash = null;
                    Messages.Group.SendGroupToAll(groupName, go);
                    SaveGroup(groupName);
                    return true;
                }
                return SetGroupPassword(null, groupName, Common.CalculateSHA256HashFromString(password));
            }
        }

        /// <summary>
        /// Sets the group password, with an unsalted SHA256 password. Set to null or empty string to remove the password. Returns true on success.
        /// </summary>
        public bool SetGroupPassword(ClientObject callingClient, string groupName, string passwordSHA256)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    string errorText = "Cannot set group password, " + groupName + " doesn't exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                GroupObject go = groups[groupName];
                if (passwordSHA256 == null || passwordSHA256 == "")
                {
                    go.passwordSalt = null;
                    go.passwordHash = null;
                    Messages.Group.SendGroupToAll(groupName, go);
                    SaveGroup(groupName);
                    return true;
                }
                //The salt is generated by the SHA256Sum of the current tick time
                string salt = Common.CalculateSHA256HashFromString(DateTime.UtcNow.Ticks.ToString());
                string saltedPassword = Common.CalculateSHA256HashFromString(salt + passwordSHA256);
                return SetGroupPassword(callingClient, groupName, salt, saltedPassword);
            }
        }

        /// <summary>
        /// Sets the group password, with a specified salt and salted password. Returns true on success
        /// </summary>
        public bool SetGroupPassword(ClientObject callingClient, string groupName, string saltSHA256, string saltedPasswordSHA256)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    string errorText = "Cannot set group password, " + groupName + " doesn't exist";
                    DarkLog.Debug(errorText);
                    Messages.Chat.SendChatMessageToClient(callingClient, errorText);
                    return false;
                }
                GroupObject go = groups[groupName];
                if (saltedPasswordSHA256 == null || saltedPasswordSHA256 == "")
                {
                    go.passwordSalt = null;
                    go.passwordHash = null;
                    Messages.Group.SendGroupToAll(groupName, go);
                    SaveGroup(groupName);
                    return true;
                }
                go.passwordSalt = saltSHA256;
                go.passwordHash = saltedPasswordSHA256;
                DarkLog.Debug("Password of group " + groupName + " changed");
                Messages.Chat.SendChatMessageToClient(callingClient, "You changed the password of " + groupName);
                Messages.Group.SendGroupToAll(groupName, go);
                SaveGroup(groupName);
                return true;
            }
        }

        /// <summary>
        /// Sets the group privacy. Set SHAPassword to null to remove the password. Returns true on success
        /// </summary>
        public bool SetGroupPrivacy(ClientObject callingClient, string groupName, GroupPrivacy groupPrivacy)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    DarkLog.Debug("Cannot set group privacy, " + groupName + " doesn't exist");
                    return false;
                }
                GroupObject go = groups[groupName];
                go.privacy = groupPrivacy;
                Messages.Group.SendGroupToAll(groupName, go);
                SaveGroup(groupName);
                return true;
            }
        }

        /// <summary>
        /// Checks the group password for a match (Raw password). Returns true on success. Always returns false if the group password is not set.
        /// </summary>
        public bool CheckGroupPasswordRaw(string groupName, string rawPassword)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    return false;
                }
                GroupObject go = groups[groupName];
                if (go.passwordSalt == null)
                {
                    return false;
                }
                if (go.passwordHash == null)
                {
                    return false;
                }
                return CheckGroupPassword(groupName, Common.CalculateSHA256HashFromString(rawPassword));
            }
        }

        /// <summary>
        /// Checks the group password for a match (Raw password). Returns true on success. Always returns false if the group password is not set.
        /// </summary>
        public bool CheckGroupPassword(string groupName, string shaPassword)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    return false;
                }
                GroupObject go = groups[groupName];
                if (go.passwordSalt == null)
                {
                    return false;
                }
                if (go.passwordHash == null)
                {
                    return false;
                }
                string checkPassword = Common.CalculateSHA256HashFromString(go.passwordSalt + shaPassword);
                return CheckGroupPassword(groupName, go.passwordSalt, checkPassword);
            }
        }

        /// <summary>
        /// Checks the group password for a match (Raw password). Returns true on success. Always returns false if the group password is not set.
        /// </summary>
        public bool CheckGroupPassword(string groupName, string saltSHA256, string saltedPasswordSHA256)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    return false;
                }
                GroupObject go = groups[groupName];
                if (go.passwordSalt == null)
                {
                    return false;
                }
                if (go.passwordHash == null)
                {
                    return false;
                }
                return (go.passwordSalt == saltSHA256 && go.passwordHash == saltedPasswordSHA256);
            }
        }

        /// <summary>
        /// Returns the group privacy. If the group does not exist, returns PUBLIC
        /// </summary>
        public GroupPrivacy GetGroupPrivacy(string groupName)
        {
            lock (groups)
            {
                if (!GroupExists(groupName))
                {
                    return GroupPrivacy.PUBLIC;
                }
                return groups[groupName].privacy;
            }
        }

        public static void Reset()
        {
            singleton = null;
        }
    }



    public class GroupObject
    {
        public List<string> members = new List<string>();
        public GroupPrivacy privacy = GroupPrivacy.PUBLIC;
        public string passwordSalt;
        public string passwordHash;
    }
}

