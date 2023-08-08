using System;
using System.Collections.Generic;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Contracts.BingoGameContract;

namespace AElf.Contracts.BingoTownContract
{
    /// <summary>
    /// The C# implementation of the contract defined in bingo_town_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public class BingoTownContract : BingoTownContractContainer.BingoTownContractBase
    {
        /// <summary>
        /// The implementation of the Hello method. It takes no parameters and returns on of the custom data types
        /// defined in the protobuf definition file.
        /// </summary>
        /// <param name="input">Empty message (from Protobuf)</param>
        /// <returns>a HelloReturn</returns>
       

        public override Empty Initialize(Empty input)
        {
            if (State.Initialized.Value)
            {
                return new Empty();
            }

            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.ConsensusContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
            State.Admin.Value = Context.Sender;
            State.GameLimitSettings.Value = new GameLimitSettings()
            {
              DailyMaxPlayCount  = BingoTownContractConstants.DailyMaxPlayCount,
              DailyPlayCountResetHours  = BingoTownContractConstants.DailyPlayCountResetHours
            };
            State.GridTypeList.Value = new GridTypeList{Value = {GridType.Blue,GridType.Gold,GridType.Red,GridType.Blue,
                GridType.Red,GridType.Blue,GridType.Gold,GridType.Red,GridType.Blue,GridType.Red,GridType.Blue,GridType.Blue,
                GridType.Gold,GridType.Red,GridType.Blue,GridType.Blue,GridType.Red,GridType.Blue}};
            State.Initialized.Value = true;
            return new Empty();
        }


        private void InitPlayerInfo()
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BingoTownContractConstants.BeanPassSymbol,
                Owner = Context.Sender,
            });
            Assert(getBalanceOutput.Balance > 0, "BeanPass Balance is not enough");
            var gameLimitSettings = State.GameLimitSettings.Value;
            var playerInformation = State.PlayerInformation[Context.Sender];
            if (playerInformation == null)
            {
                playerInformation = new PlayerInformation
                {
                    PlayerAddress = Context.Sender,
                    LastPlayTime = Context.CurrentBlockTime,
                    SumScore = 0,
                    CurGridNum = 0,
                    PlayableCount = gameLimitSettings.DailyMaxPlayCount
                };
            }
            else
            {
                playerInformation.PlayableCount = GetPlayableCount(gameLimitSettings, playerInformation);
                playerInformation.LastPlayTime = Context.CurrentBlockTime;
            }

            Assert(playerInformation.PlayableCount > 0,"PlayableCount is not enough");
            playerInformation.PlayableCount--;
            State.PlayerInformation[Context.Sender] = playerInformation;
        }

        public override PlayOutput Play(PlayInput input)
        {
            InitPlayerInfo();
            var boutInformation = new BoutInformation
            {
                PlayBlockHeight = Context.CurrentHeight,
                PlayId = Context.OriginTransactionId,
                PlayTime = Context.CurrentBlockTime,
                PlayerAddress = Context.Sender
            };
            State.BoutInformation[Context.OriginTransactionId] = boutInformation;
            return new PlayOutput { ExpectedBlockHeight = Context.CurrentHeight.Add(BingoTownContractConstants.BingoBlockHeight) };
        } 

        public override Empty Bingo(Hash input)
        {
            Context.LogDebug(() => $"Getting game result of play id: {input.ToHex()}");

            checkBingo(input,out var playerInformation, out var boutInformation, out var targetHeight);
            var randomHash = State.ConsensusContract.GetRandomHash.Call(new Int64Value
            {
                Value = targetHeight
            });

            Assert(randomHash != null && !randomHash.Value.IsNullOrEmpty(),
                "Still preparing your game result, please wait for a while :)");

            SetBoutInformationBingoInfo(input, randomHash, playerInformation, boutInformation);
            SetPlayerInformation(playerInformation, boutInformation);
            Context.Fire(new Bingoed
            {
                PlayBlockHeight = boutInformation.PlayBlockHeight,
                GridType = boutInformation.GridType,
                GridNum = boutInformation.GridNum,
                Score = boutInformation.Score,
                IsComplete = boutInformation.IsComplete,
                PlayId = boutInformation.PlayId,
                BingoBlockHeight = boutInformation.BingoBlockHeight,
                PlayTime = boutInformation.PlayTime,
                PlayerAddress = boutInformation.PlayerAddress
            });
            return new Empty();
        }

        private  void SetPlayerInformation(PlayerInformation playerInformation, BoutInformation boutInformation)
        {
            playerInformation.CurGridNum = (playerInformation.CurGridNum+boutInformation.GridNum) % State.GridTypeList.Value.CalculateSize();
            playerInformation.SumScore += boutInformation.Score;
            State.PlayerInformation[boutInformation.PlayerAddress] = playerInformation;
        }

        private void SetBoutInformationBingoInfo(Hash input, Hash randomHash, PlayerInformation playerInformation,
            BoutInformation boutInformation)
        {
            var randomNum = Convert.ToInt32(Math.Abs(randomHash.ToInt64()) % 6 + 1);
            var curGridNum = playerInformation.CurGridNum + randomNum;
            var gridType = State.GridTypeList.Value.Value[curGridNum];
            boutInformation.Score = GetScoreByGridType(input, gridType, randomHash);
            boutInformation.IsComplete = true;
            boutInformation.GridNum = randomNum;
            boutInformation.GridType = gridType;
            boutInformation.BingoBlockHeight = Context.CurrentHeight;
            State.BoutInformation[input] = boutInformation;
        }

        private  Int32 GetScoreByGridType(Hash input, GridType gridType, Hash randomHash)
        {
            int score;
            if (gridType == GridType.Blue)
            {
                score = BingoTownContractConstants.BlueGridScore;
            }
            else if (gridType == GridType.Red)
            {
                score = BingoTownContractConstants.RedGridScore;
            }
            else
            {
                var scoreHash = HashHelper.ConcatAndCompute(randomHash, input);
                score = Convert.ToInt32(Math.Abs(randomHash.ToInt64()) % 20 + 30);
            }
            return score;
        }

        private void checkBingo(Hash input,out PlayerInformation playerInformation, out BoutInformation boutInformation, out long targetHeight)
        {
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

             playerInformation = State.PlayerInformation[Context.Sender];

            Assert(playerInformation != null, $"User {Context.Sender} not Login before.");

            boutInformation = State.BoutInformation[input];

            Assert(boutInformation != null, "Bout not found.");

            Assert(!boutInformation!.IsComplete, "Bout already finished.");

            Assert(boutInformation.PlayerAddress == Context.Sender, "Only the player can get the result.");

            targetHeight = boutInformation.PlayBlockHeight.Add(BingoTownContractConstants.BingoBlockHeight);
            Assert(targetHeight <= Context.CurrentHeight, "Invalid target height.");
        }

        public override Empty ChangeAdmin(Address input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input != null, "Invalid input.");

            if (State.Admin.Value == input)
            {
                return new Empty();
            }

            State.Admin.Value = input;
            return new Empty();
        }
        
        public override Address GetAdmin(Empty input)
        {
            return State.Admin.Value;
        }

        public override BoutInformation GetBoutInformation(GetBoutInformationInput input)
        {
            Assert(input != null, "Invalid input.");
            Assert(input!.PlayId != null && !input.PlayId.Value.IsNullOrEmpty(), "Invalid playId.");

            var boutInformation = State.BoutInformation[input.PlayId];

            Assert(boutInformation != null, "Bout not found.");

            return boutInformation;
        }

        public override PlayerInformation GetPlayerInformation(Address input)
        {
            Assert(input != null, "Invalid input.");
            var playerInformation =  State.PlayerInformation[input];
            Assert(playerInformation !=null,"playerInformation not found.");
            var gameLimitSettings = State.GameLimitSettings.Value;
            playerInformation.PlayableCount = GetPlayableCount(gameLimitSettings, playerInformation);
            return playerInformation;
        
        }

        private Int32 GetPlayableCount(GameLimitSettings gameLimitSettings, PlayerInformation playerInformation)
        {
            var now = Context.CurrentBlockTime.ToDateTime();
           var playCountResetDateTime =
               new DateTime(now.Year, now.Month, now.Day, gameLimitSettings.DailyPlayCountResetHours, 0, 0,DateTimeKind.Utc).ToTimestamp();
            if (playerInformation.LastPlayTime.CompareTo(playCountResetDateTime) == -1)
            {
                return gameLimitSettings.DailyMaxPlayCount;
            }
            return playerInformation.PlayableCount;
        }

        public override GameLimitSettings GetGameLimitSettings(Empty input)
        {
            return State.GameLimitSettings.Value;
        }

        public override Empty SetGameLimitSettings(GameLimitSettings input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input.DailyPlayCountResetHours is >= 0 and < 24, "Invalid input.");
            Assert(input.DailyMaxPlayCount  >= 0, "Invalid input.");
            State.GameLimitSettings.Value = input;
            return new Empty();
        }
    }
    
    
}