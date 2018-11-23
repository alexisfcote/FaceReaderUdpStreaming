﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FaceReaderAPI;
using FaceReaderAPI.Data;
using System.Net.Sockets;
using System.Net;
using Newtonsoft.Json;

namespace FaceReaderExternalLoggingSample
{
    public partial class ExternalLogging : Form
    {
        // global instance of the FaceReaderController
        private FaceReaderController mFaceReaderController;

        public ExternalLogging()
        {
            InitializeComponent();       
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // if there is a connection, disconnect
            if (mFaceReaderController != null)
                mFaceReaderController.DisconnectFromFaceReader();
        }

        #region ButtonActions

        private void btnConnect_Click(object sender, EventArgs e)
        {
            // if the FaceReaderController already exists, Dispose it. Note that Dispose also Disconnects
            if (mFaceReaderController != null)
            {
                mFaceReaderController.Dispose();
                mFaceReaderController = null;
            }

            // create a new instance of FaceReaderDataReceiver, with the ipaddress and the port
            mFaceReaderController = new FaceReaderController(txtIPAdress.Text, (int)numPort.Value);
            try
            {
                // register the events
                mFaceReaderController.ClassificationReceived += 
                    new EventHandler<ClassificationEventArgs>(a_faceReaderController_ClassificationReceived);

                mFaceReaderController.Disconnected += 
                    new EventHandler(a_faceReaderController_Disconnected);

                mFaceReaderController.Connected += 
                    new EventHandler(a_faceReaderController_Connected);

                mFaceReaderController.ActionSucceeded += 
                    new EventHandler<MessageEventArgs>(a_faceReaderController_ActionSucceeded);

                mFaceReaderController.ErrorOccured += 
                    new EventHandler<ErrorEventArgs>(a_faceReaderController_ErrorOccured);


                // connect to FaceReader. If the connection was succesful, Connected will fire, otherwise Disconnected will fire
                mFaceReaderController.ConnectToFaceReader();
            }
            catch (Exception ex)
            {
                WriteInfo(rtbMessages, ex.Message);
            }
            mFaceReaderController.StartLogSending(LogType.DetailedLog);
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (mFaceReaderController != null)
            {
                // if there is a connection, disconnect
                if (mFaceReaderController.IsConnected)
                    mFaceReaderController.DisconnectFromFaceReader();
                else
                    WriteInfo(rtbMessages, "There is no connection to disconnect");
            }
            mFaceReaderController = null;
        }


        private void btnStartAnalysis_Click(object sender, EventArgs e)
        {
            if (mFaceReaderController != null)
                mFaceReaderController.StartAnalyzing();
        }

        private void btnStopAnalysis_Click(object sender, EventArgs e)
        {
            if (mFaceReaderController != null)
                mFaceReaderController.StopAnalyzing();
        }


        #endregion

        #region EventHandlers

        void a_faceReaderController_ErrorOccured(object sender, ErrorEventArgs e)
        {
            WriteInfo(rtbMessages, "Error occured\t-> " + e.Exception.Message);
        }

        void a_faceReaderController_ActionSucceeded(object sender, MessageEventArgs e)
        {
            WriteInfo(rtbMessages, "Action Succeeded\t-> " + e.Message);
        }

        void a_faceReaderController_Connected(object sender, EventArgs e)
        {
            WriteInfo(rtbMessages, "Connection to FaceReader was succesfull");
        }

        void a_faceReaderController_Disconnected(object sender, EventArgs e)
        {
            WriteInfo(rtbMessages, "Disconnected");
        }

        void a_faceReaderController_ClassificationReceived(object sender, ClassificationEventArgs e)
        {
            // get the classification from the event arguments
            FaceReaderAPI.Data.Classification classification = e.Classification;
            
            // if a classification was received
            if (classification != null)
            {
                // if the classification is in the form of a StateLogs
                if (classification.LogType == FaceReaderAPI.Data.LogType.StateLog)
                {
                }
                // if the classification is in the form of a DetailedLog
                else
                {
                    // show the information
                    WriteInfo(rtbDetailedClassification, classification.ToString());
                    Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                    IPAddress serverAddr = IPAddress.Parse("127.0.0.1");

                    IPEndPoint endPoint = new IPEndPoint(serverAddr, 5556);

                    Dictionary<string, string> valuedict = new Dictionary<string, string>();

                    foreach (ClassificationValue classificationvalue in classification.ClassificationValues)
                    {
                        if (classificationvalue.Value.Count() > 0)
                        {
                            if (classificationvalue.Value.Count() > 1)
                            {
                                valuedict.Add(classificationvalue.Label, JsonConvert.SerializeObject(classificationvalue.Value));
                            }
                            else
                            {
                                valuedict.Add(classificationvalue.Label, classificationvalue.Value[0].ToString());
                            }
                        }
                        else
                        {
                            if (classificationvalue.Value.Count() > 1)
                            {
                                valuedict.Add(classificationvalue.Label, JsonConvert.SerializeObject(classificationvalue.State));
                            }
                            else
                            {
                                valuedict.Add(classificationvalue.Label, classificationvalue.State[0].ToString());
                            }
                        }
                    }


                    string json = JsonConvert.SerializeObject(valuedict);

                    string text = classification.ToString();
                    byte[] send_buffer = Encoding.UTF8.GetBytes(json);

                    sock.SendTo(send_buffer, endPoint);
                }
            }
        }

        #endregion

        #region HelperFunctions

        // delegate for writing to the Gui from another thread
        public delegate void GuiCallback<T>(T obj);
        public delegate void GuiCallback<T1, T2>(T1 obj1, T2 obj2);

        
        /// <summary>
        /// Helper function to write information to a RichTextBox, in backwards order, with maximum of
        /// 100 lines
        /// </summary>
        /// <param name="rtb"></param>
        /// <param name="info"></param>
        private void WriteInfo(RichTextBox rtb, string info)
        {
            if (rtb.InvokeRequired)
            {
                GuiCallback<RichTextBox, string> callback = new GuiCallback<RichTextBox, string>(WriteInfo);
                rtbDetailedClassification.Invoke(callback, rtb, info);
            }
            else
            {
                rtb.Text = info + "\n" + rtb.Text;

                while(rtb.Lines.Length > 100)
                {
                    string[] lines = new string[rtb.Lines.Length - 1];
                    Array.Copy(rtb.Lines, 0, lines, 0, lines.Length);

                    rtb.Lines = lines;
                }
            }
        }

        private void AddToCombobox(ComboBox cmb, string[] items)
        {
            if (cmb.InvokeRequired)
            {
                GuiCallback<ComboBox, string[]> callback = new GuiCallback<ComboBox, string[]>(AddToCombobox);
                cmb.Invoke(callback, cmb, items);
            }
            else
            {
                cmb.Items.Clear();
                
                if(items != null)
                    cmb.Items.AddRange(items);
                
                if(cmb.Items.Count > 0)
                    cmb.SelectedItem = cmb.Items[0];
            }
        }

        private string ToMultilineString(string[] strArray)
        {
            string txt = "";
            foreach (string s in strArray)
                txt += s + "\n";

            return txt;
        }

        #endregion
    }
}
