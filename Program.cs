using System;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static string mnemonic = "toddler private insect clog auto panic good caught shop used lunar worry";

        static void Main(string[] args)
        {
            Console.WriteLine(GenerateAddress(mnemonic));
        }

        public static string GenerateMnemonic()
        {
            return string.Join(' ', new Mnemonic(Wordlist.English, WordCount.Twelve).Words); 
        }

        public static string GenerateAddress(string mnemonic)
        {
            var mnemo = new Mnemonic(mnemonic, Wordlist.English);
            var hdroot = mnemo.DeriveExtKey();
            var pKey = hdroot.Derive(new KeyPath("m/84'/0'/0'/0/0"));
            return pKey.PrivateKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString();
        }


        public static bool SendBTC(string secret, string toAddress, decimal amount, string fundingTransactionHash, decimal minerFeeAmount)
        {
            Network bitcoinNetwork = Network.Main;
            var bitcoinPrivateKey = new BitcoinSecret(secret, bitcoinNetwork);
            var address = bitcoinPrivateKey.GetAddress(ScriptPubKeyType.Legacy);

            var client = new QBitNinjaClient(bitcoinNetwork);
            var transactionId = uint256.Parse(fundingTransactionHash);
            var transactionResponse = client.GetTransaction(transactionId).Result;

            var receivedCoins = transactionResponse.ReceivedCoins;

            OutPoint outPointToSpend = null;
            foreach (var coin in receivedCoins)
            {
                if (coin.TxOut.ScriptPubKey == bitcoinPrivateKey.GetAddress(ScriptPubKeyType.Legacy).ScriptPubKey)
                {
                    outPointToSpend = coin.Outpoint;
                }
            }

            var transaction = Transaction.Create(bitcoinNetwork);
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = outPointToSpend
            });

            var receiverAddress = BitcoinAddress.Create(toAddress, bitcoinNetwork);


            var txOutAmount = new Money(amount, MoneyUnit.BTC);

            // Tx fee
            var minerFee = new Money(minerFeeAmount, MoneyUnit.BTC);

            // Change
            var txInAmount = (Money)receivedCoins[(int)outPointToSpend.N].Amount;
            var changeAmount = txInAmount - txOutAmount - minerFee;

            transaction.Outputs.Add(txOutAmount, receiverAddress.ScriptPubKey);
            transaction.Outputs.Add(changeAmount, bitcoinPrivateKey.GetAddress(ScriptPubKeyType.Legacy).ScriptPubKey);


            transaction.Inputs[0].ScriptSig = address.ScriptPubKey;

            //Sign Tx
            transaction.Sign(bitcoinPrivateKey, receivedCoins.ToArray());

            //Broadcast Tx
            BroadcastResponse broadcastResponse = client.Broadcast(transaction).Result;

            return broadcastResponse.Success;
        }
    }
}