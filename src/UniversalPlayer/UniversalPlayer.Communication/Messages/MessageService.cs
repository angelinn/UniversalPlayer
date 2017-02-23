//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Media.Playback;

namespace BackgroundAudioShared.Messages
{
    public static class MessageService
    {
        private const string MessageType = "MessageType";
        private const string MessageBody = "MessageBody";

        public static void SendMessageToForeground<T>(T message)
        {
            var payload = new ValueSet();
            payload.Add(MessageService.MessageType, typeof(T).FullName);
            payload.Add(MessageService.MessageBody, JsonHelper.ToJson(message));
            BackgroundMediaPlayer.SendMessageToForeground(payload);
        }
    
        public static void SendMessageToBackground<T>(T message)
        {
            var payload = new ValueSet();
            payload.Add(MessageService.MessageType, typeof(T).FullName);
            payload.Add(MessageService.MessageBody, JsonHelper.ToJson(message));
            BackgroundMediaPlayer.SendMessageToBackground(payload);
        }

        public static bool TryParseMessage<T>(ValueSet valueSet, out T message)
        {
            object messageTypeValue;
            object messageBodyValue;

            message = default(T);

            // Get message payload
            if (valueSet.TryGetValue(MessageService.MessageType, out messageTypeValue)
                && valueSet.TryGetValue(MessageService.MessageBody, out messageBodyValue))
            {
                // Validate type
                if ((string)messageTypeValue != typeof(T).FullName)
                {
                    Debug.WriteLine("Message type was {0} but expected type was {1}", (string)messageTypeValue, typeof(T).FullName);
                    return false;
                }

                message = JsonHelper.FromJson<T>(messageBodyValue.ToString());
                return true;
            }

            return false;
        }
    }
}
