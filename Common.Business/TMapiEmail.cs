﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Globalization;

namespace Common.Business
{
    public class TMapiEmail
    {

        #region SESSION
        // ----------------------------------------------------------- SESSION ---------

        public bool Logon(IntPtr hwnd)
        {
            winhandle = hwnd;
            error = MAPILogon(hwnd, null, null, 0, 0, ref session);
            if (error != 0)
                error = MAPILogon(hwnd, null, null, MapiLogonUI, 0, ref session);
            return error == 0;
        }

        public void Reset()
        {
            findseed = null;
            origin = new MapiRecipDesc();
            recpts.Clear();
            attachs.Clear();
            lastMsg = null;
        }

        public void Logoff()
        {
            if (session != IntPtr.Zero)
            {
                error = MAPILogoff(session, winhandle, 0, 0);
                session = IntPtr.Zero;
            }
        }


        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        private static extern int MAPILogon(IntPtr hwnd, string prf, string pw,
                                                int flg, int rsv, ref IntPtr sess);
        [DllImport("MAPI32.DLL")]
        private static extern int MAPILogoff(IntPtr sess, IntPtr hwnd,
                                                int flg, int rsv);

        private const int MapiLogonUI = 0x00000001;
        private const int MapiPasswordUI = 0x00020000;
        private const int MapiNewSession = 0x00000002;
        private const int MapiDialog = 0x00000008;
        private const int MapiForceDownload = 0x00001000;
        private const int MapiExtendedUI = 0x00000020;


        private IntPtr session = IntPtr.Zero;
        private IntPtr winhandle = IntPtr.Zero;
        #endregion

        #region SENDING
        // ----------------------------------------------------------- SENDING ---------

        public bool Send(string sub, string txt, out string aErrInfo)
        {
            lastMsg = new MapiMessage();
            lastMsg.subject = sub;
            lastMsg.noteText = txt;

            // set pointers
            lastMsg.originator = AllocOrigin();
            lastMsg.recips = AllocRecips(out lastMsg.recipCount);
            lastMsg.files = AllocAttachs(out lastMsg.fileCount);

            //error = MAPISendMail(session, winhandle, lastMsg, 0, 0);
            error = MAPISendMail(session, winhandle, lastMsg, MapiDialog, 0);
            Dealloc();
            Reset();

            aErrInfo = GetSendMailErr(error);  // get error details by error code
            //return (error == 0);
            return (error == SUCCESS_SUCCESS) || (error == MAPI_USER_ABORT);  // 0 = SUCCESS_SUCCESS; 1 = MAPI_USER_ABORT
        }

        public void AddRecip(string name, string addr, bool cc)
        {
            MapiRecipDesc dest = new MapiRecipDesc();
            if (cc)
                dest.recipClass = MapiCC;
            else
                dest.recipClass = MapiTO;
            dest.name = name;
            dest.address = addr;
            recpts.Add(dest);
        }

        public void SetSender(string sname, string saddr)
        {
            origin.name = sname;
            origin.address = saddr;
        }

        public void Attach(string filepath)
        {
            attachs.Add(filepath);
        }

        private IntPtr AllocOrigin()
        {
            origin.recipClass = MapiORIG;
            Type rtype = typeof(MapiRecipDesc);
            int rsize = Marshal.SizeOf(rtype);
            IntPtr ptro = Marshal.AllocHGlobal(rsize);
            Marshal.StructureToPtr(origin, ptro, false);
            return ptro;
        }

        private IntPtr AllocRecips(out int recipCount)
        {
            recipCount = 0;
            if (recpts.Count == 0)
                return IntPtr.Zero;

            Type rtype = typeof(MapiRecipDesc);
            int rsize = Marshal.SizeOf(rtype);
            IntPtr ptrr = Marshal.AllocHGlobal(recpts.Count * rsize);

            int runptr = (int)ptrr;
            for (int i = 0; i < recpts.Count; i++)
            {
                Marshal.StructureToPtr(recpts[i] as MapiRecipDesc, (IntPtr)runptr, false);
                runptr += rsize;
            }

            recipCount = recpts.Count;
            return ptrr;
        }

        private IntPtr AllocAttachs(out int fileCount)
        {
            fileCount = 0;
            if (attachs == null)
                return IntPtr.Zero;
            if ((attachs.Count <= 0) || (attachs.Count > 100))
                return IntPtr.Zero;

            Type atype = typeof(MapiFileDesc);
            int asize = Marshal.SizeOf(atype);
            IntPtr ptra = Marshal.AllocHGlobal(attachs.Count * asize);

            MapiFileDesc mfd = new MapiFileDesc();
            mfd.position = -1;
            int runptr = (int)ptra;
            for (int i = 0; i < attachs.Count; i++)
            {
                string path = attachs[i] as string;
                mfd.name = Path.GetFileName(path);
                mfd.path = path;
                Marshal.StructureToPtr(mfd, (IntPtr)runptr, false);
                runptr += asize;
            }

            fileCount = attachs.Count;
            return ptra;
        }

        private void Dealloc()
        {
            Type rtype = typeof(MapiRecipDesc);
            int rsize = Marshal.SizeOf(rtype);

            if (lastMsg.originator != IntPtr.Zero)
            {
                Marshal.DestroyStructure(lastMsg.originator, rtype);
                Marshal.FreeHGlobal(lastMsg.originator);
            }

            if (lastMsg.recips != IntPtr.Zero)
            {
                int runptr = (int)lastMsg.recips;
                for (int i = 0; i < lastMsg.recipCount; i++)
                {
                    Marshal.DestroyStructure((IntPtr)runptr, rtype);
                    runptr += rsize;
                }
                Marshal.FreeHGlobal(lastMsg.recips);
            }

            if (lastMsg.files != IntPtr.Zero)
            {
                Type ftype = typeof(MapiFileDesc);
                int fsize = Marshal.SizeOf(ftype);

                int runptr = (int)lastMsg.files;
                for (int i = 0; i < lastMsg.fileCount; i++)
                {
                    Marshal.DestroyStructure((IntPtr)runptr, ftype);
                    runptr += fsize;
                }
                Marshal.FreeHGlobal(lastMsg.files);
            }
        }

        private const int MapiORIG = 0;
        private const int MapiTO = 1;
        private const int MapiCC = 2;
        private const int MapiBCC = 3;

        [DllImport("MAPI32.DLL")]
        private static extern int MAPISendMail(IntPtr sess, IntPtr hwnd,
                                                MapiMessage message,
                                                int flg, int rsv);

        // Return values from MAPISendMail functions - begin
        private const int SUCCESS_SUCCESS = 0;  // The call succeeded and the message was sent.
        private const int MAPI_USER_ABORT = 1;
        private const int MAPI_E_USER_ABORT = MAPI_USER_ABORT;  // The user canceled one of the dialog boxes. No message was sent.
        private const int MAPI_E_FAILURE = 2;  // One or more unspecified errors occurred. No message was sent.
        private const int MAPI_E_LOGIN_FAILURE = 3;  // There was no default logon, and the user failed to log on successfully when the logon dialog box was displayed. No message was sent.
        private const int MAPI_E_INSUFFICIENT_MEMORY = 5;  // There was insufficient memory to proceed. No message was sent.
        private const int MAPI_E_TOO_MANY_FILES = 9;  // There were too many file attachments. No message was sent.
        private const int MAPI_E_TOO_MANY_RECIPIENTS = 10;  // There were too many recipients. No message was sent.
        private const int MAPI_E_ATTACHMENT_NOT_FOUND = 11;  // The specified attachment was not found. No message was sent.
        private const int MAPI_E_ATTACHMENT_OPEN_FAILURE = 12;  // The specified attachment could not be opened. No message was sent.
        private const int MAPI_E_UNKNOWN_RECIPIENT = 14;  // A recipient did not appear in the address list. No message was sent.
        private const int MAPI_E_BAD_RECIPTYPE = 15;  // The type of a recipient was not MAPI_TO, MAPI_CC, or MAPI_BCC. No message was sent.
        private const int MAPI_E_TEXT_TOO_LARGE = 18;  // The text in the message was too large. No message was sent.
        private const int MAPI_E_AMBIGUOUS_RECIPIENT = 21;  // A recipient matched more than one of the recipient descriptor structures and MAPI_DIALOG was not set. No message was sent.
        private const int MAPI_E_INVALID_RECIPS = 25;  // One or more recipients were invalid or did not resolve to any address.
        // Return values from MAPISendMail functions - end


        private string GetSendMailErr(int errCode)
        {
            switch (errCode)
            {
                case 1:
                    return "the user canceled one of the dialog boxes.";
                case 2:
                    return "one or more unspecified errors occurred.";
                case 3:
                    return "there was no default logon, and the user failed to log on successfully when the logon dialog box was displayed.";
                case 5:
                    return "there was insufficient memory to proceed.";
                case 9:
                    return "there were too many file attachments.";
                case 10:
                    return "there were too many recipients.";
                case 11:
                    return "the specified attachment was not found.";
                case 12:
                    return "the specified attachment could not be opened.";
                case 14:
                    return "a recipient did not appear in the address list.";
                case 15:
                    return "the type of a recipient was not MAPI_TO, MAPI_CC, or MAPI_BCC.";
                case 18:
                    return "the text in the message was too large.";
                case 21:
                    return "a recipient matched more than one of the recipient descriptor structures and MAPI_DIALOG was not set.";
                case 25:
                    return "one or more recipients were invalid or did not resolve to any address.";
                default:
                    return "no Mail Client was found.";
            }
        }

        private MapiRecipDesc origin = new MapiRecipDesc();
        private ArrayList recpts = new ArrayList();
        private ArrayList attachs = new ArrayList();
        #endregion

        #region FINDING
        // ----------------------------------------------------------- FINDING ---------

        public bool Next(ref MailEnvelop env)
        {
            error = MAPIFindNext(session, winhandle, null, findseed,
                                    MapiLongMsgID, 0, lastMsgID);
            if (error != 0)
                return false;
            findseed = lastMsgID.ToString();

            IntPtr ptrmsg = IntPtr.Zero;
            error = MAPIReadMail(session, winhandle, findseed,
                                MapiEnvOnly | MapiPeek | MapiSuprAttach, 0, ref ptrmsg);
            if ((error != 0) || (ptrmsg == IntPtr.Zero))
                return false;

            lastMsg = new MapiMessage();
            Marshal.PtrToStructure(ptrmsg, lastMsg);
            MapiRecipDesc orig = new MapiRecipDesc();
            if (lastMsg.originator != IntPtr.Zero)
                Marshal.PtrToStructure(lastMsg.originator, orig);

            env.id = findseed;
            env.date = DateTime.ParseExact(lastMsg.dateReceived, "yyyy/MM/dd HH:mm", DateTimeFormatInfo.InvariantInfo);
            env.subject = lastMsg.subject;
            env.from = orig.name;
            env.unread = (lastMsg.flags & MapiUnread) != 0;
            env.atts = lastMsg.fileCount;

            error = MAPIFreeBuffer(ptrmsg);
            return error == 0;
        }


        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        private static extern int MAPIFindNext(IntPtr sess, IntPtr hwnd, string typ,
                                                string seed, int flg, int rsv, StringBuilder id);

        private const int MapiUnreadOnly = 0x00000020;
        private const int MapiGuaranteeFiFo = 0x00000100;
        private const int MapiLongMsgID = 0x00004000;

        private StringBuilder lastMsgID = new StringBuilder(600);
        private string findseed = null;
        #endregion

        #region READING
        // ----------------------------------------------------------- READING ---------

        public string Read(string id, out MailAttach[] aat)
        {
            aat = null;
            IntPtr ptrmsg = IntPtr.Zero;
            error = MAPIReadMail(session, winhandle, id,
                                    MapiPeek | MapiSuprAttach, 0, ref ptrmsg);
            if ((error != 0) || (ptrmsg == IntPtr.Zero))
                return null;

            lastMsg = new MapiMessage();
            Marshal.PtrToStructure(ptrmsg, lastMsg);

            if ((lastMsg.fileCount > 0) && (lastMsg.fileCount < 100) && (lastMsg.files != IntPtr.Zero))
                GetAttachNames(out aat);

            MAPIFreeBuffer(ptrmsg);
            return lastMsg.noteText;
        }

        public bool Delete(string id)
        {
            error = MAPIDeleteMail(session, winhandle, id, 0, 0);
            return error == 0;
        }

        public bool SaveAttachm(string id, string name, string savepath)
        {
            IntPtr ptrmsg = IntPtr.Zero;
            error = MAPIReadMail(session, winhandle, id,
                                    MapiPeek, 0, ref ptrmsg);
            if ((error != 0) || (ptrmsg == IntPtr.Zero))
                return false;

            lastMsg = new MapiMessage();
            Marshal.PtrToStructure(ptrmsg, lastMsg);
            bool f = false;
            if ((lastMsg.fileCount > 0) && (lastMsg.fileCount < 100) && (lastMsg.files != IntPtr.Zero))
                f = SaveAttachByName(name, savepath);
            MAPIFreeBuffer(ptrmsg);
            return f;
        }


        private void GetAttachNames(out MailAttach[] aat)
        {
            aat = new MailAttach[lastMsg.fileCount];
            Type fdtype = typeof(MapiFileDesc);
            int fdsize = Marshal.SizeOf(fdtype);
            MapiFileDesc fdtmp = new MapiFileDesc();
            int runptr = (int)lastMsg.files;
            for (int i = 0; i < lastMsg.fileCount; i++)
            {
                Marshal.PtrToStructure((IntPtr)runptr, fdtmp);
                runptr += fdsize;
                aat[i] = new MailAttach();
                if (fdtmp.flags == 0)
                {
                    aat[i].position = fdtmp.position;
                    aat[i].name = fdtmp.name;
                    aat[i].path = fdtmp.path;
                }
            }
        }


        private bool SaveAttachByName(string name, string savepath)
        {
            bool f = true;
            Type fdtype = typeof(MapiFileDesc);
            int fdsize = Marshal.SizeOf(fdtype);
            MapiFileDesc fdtmp = new MapiFileDesc();
            int runptr = (int)lastMsg.files;
            for (int i = 0; i < lastMsg.fileCount; i++)
            {
                Marshal.PtrToStructure((IntPtr)runptr, fdtmp);
                runptr += fdsize;
                if (fdtmp.flags != 0)
                    continue;
                if (fdtmp.name == null)
                    continue;

                try
                {
                    if (name == fdtmp.name)
                    {
                        if (File.Exists(savepath))
                            File.Delete(savepath);
                        File.Move(fdtmp.path, savepath);
                    }
                }
                catch (Exception)
                { f = false; error = 13; }

                try
                {
                    File.Delete(fdtmp.path);
                }
                catch (Exception)
                { }
            }
            return f;
        }

        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        private static extern int MAPIReadMail(IntPtr sess, IntPtr hwnd, string id,
                                                int flg, int rsv, ref IntPtr ptrmsg);

        [DllImport("MAPI32.DLL")]
        private static extern int MAPIFreeBuffer(IntPtr ptr);

        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        private static extern int MAPIDeleteMail(IntPtr sess, IntPtr hwnd, string id,
                                                int flg, int rsv);

        private const int MapiPeek = 0x00000080;
        private const int MapiSuprAttach = 0x00000800;
        private const int MapiEnvOnly = 0x00000040;
        private const int MapiBodyAsFile = 0x00000200;

        private const int MapiUnread = 0x00000001;
        private const int MapiReceiptReq = 0x00000002;
        private const int MapiSent = 0x00000004;

        private MapiMessage lastMsg = null;
        #endregion

        #region ADDRESS

        public bool SingleAddress(string label, out string name, out string addr)
        {
            name = null;
            addr = null;
            int newrec = 0;
            IntPtr ptrnew = IntPtr.Zero;
            error = MAPIAddress(session, winhandle, null, 1, label, 0, IntPtr.Zero,
                                    0, 0, ref newrec, ref ptrnew);
            if ((error != 0) || (newrec < 1) || (ptrnew == IntPtr.Zero))
                return false;

            MapiRecipDesc recip = new MapiRecipDesc();
            Marshal.PtrToStructure(ptrnew, recip);
            name = recip.name;
            addr = recip.address;

            MAPIFreeBuffer(ptrnew);
            return true;
        }


        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        private static extern int MAPIAddress(IntPtr sess, IntPtr hwnd, string caption,
                                                int editfld, string labels, int recipcount, IntPtr ptrrecips,
                                                int flg, int rsv, ref int newrec, ref IntPtr ptrnew);
        #endregion

        #region ERRORS
        // ----------------------------------------------------------- ERRORS ---------

        public string Error()
        {
            if (error <= 26)
                return errors[error];
            return "?unknown? [" + error.ToString() + "]";
        }

        private int error = 0;

        private readonly string[] errors = new string[] {
		"OK [0]", "User abort [1]", "General MAPI failure [2]", "MAPI login failure [3]",
		"Disk full [4]", "Insufficient memory [5]", "Access denied [6]", "-unknown- [7]",
		"Too many sessions [8]", "Too many files were specified [9]", "Too many recipients were specified [10]", "A specified attachment was not found [11]",
		"Attachment open failure [12]", "Attachment write failure [13]", "Unknown recipient [14]", "Bad recipient type [15]",
		"No messages [16]", "Invalid message [17]", "Text too large [18]", "Invalid session [19]",
		"Type not supported [20]", "A recipient was specified ambiguously [21]", "Message in use [22]", "Network failure [23]",
		"Invalid edit fields [24]", "Invalid recipients [25]", "Not supported [26]" 
		};
        #endregion
    }

    #region MAPI STRUCTURES
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class MapiMessage
    {
        public int reserved;
        public string subject;
        public string noteText;
        public string messageType;
        public string dateReceived;
        public string conversationID;
        public int flags;
        public IntPtr originator;		// MapiRecipDesc* [1]
        public int recipCount;
        public IntPtr recips;			// MapiRecipDesc* [n]
        public int fileCount;
        public IntPtr files;			// MapiFileDesc*  [n]
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class MapiRecipDesc
    {
        public int reserved;
        public int recipClass;
        public string name;
        public string address;
        public int eIDSize;
        public IntPtr entryID;			// void*
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class MapiFileDesc
    {
        public int reserved;
        public int flags;
        public int position;
        public string path;
        public string name;
        public IntPtr type;
    }
    #endregion

    #region HELPER STRUCTURES
    public class MailEnvelop
    {
        public string id;
        public DateTime date;
        public string from;
        public string subject;
        public bool unread;
        public int atts;
    }

    public class MailAttach
    {
        public int position;
        public string path;
        public string name;
    }
    #endregion
}