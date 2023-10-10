using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.BeangoTownContract
{
    /// <summary>
    /// The C# implementation of the contract defined in beango_town_contract.proto that is located in the "protobuf"
    /// </summary>
    public class BeangoTownContract : BeangoTownContractContainer.BeangoTownContractBase
    {
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
                DailyMaxPlayCount = BeangoTownContractConstants.DailyMaxPlayCount,
                DailyPlayCountResetHours = BeangoTownContractConstants.DailyPlayCountResetHours
            };
            State.GridTypeList.Value = new GridTypeList
            {
                Value =
                {
                    GridType.Blue, GridType.Blue, GridType.Red, GridType.Blue, GridType.Gold, GridType.Red,
                    GridType.Blue, GridType.Blue, GridType.Red, GridType.Gold, GridType.Blue, GridType.Red,
                    GridType.Blue, GridType.Blue, GridType.Gold, GridType.Red, GridType.Blue, GridType.Red
                }
            };
            State.Initialized.Value = true;
            return new Empty();
        }


        private void InitPlayerInfo(bool resetStart)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BeangoTownContractConstants.BeanPassSymbol,
                Owner = Context.Sender,
            });
            Assert(getBalanceOutput.Balance > 0, "BeanPass Balance is not enough");
            var playerInformation = GetCurrentPlayerInformation(Context.Sender, true);
            Assert(playerInformation.PlayableCount > 0, "PlayableCount is not enough");
            if (resetStart)
            {
                playerInformation.CurGridNum = 0;
            }
            playerInformation.PlayableCount--;
            playerInformation.LastPlayTime = Context.CurrentBlockTime;
            State.PlayerInformation[Context.Sender] = playerInformation;
        }

        public override PlayOutput Play(PlayInput input)
        {
            Assert(input.DiceCount <= 3, "Invalid diceCount");
            InitPlayerInfo(input.ResetStart);
            var expectedBlockHeight = Context.CurrentHeight.Add(BeangoTownContractConstants.BingoBlockHeight);
            var boutInformation = new BoutInformation
            {
                PlayBlockHeight = Context.CurrentHeight,
                PlayId = Context.OriginTransactionId,
                PlayTime = Context.CurrentBlockTime,
                PlayerAddress = Context.Sender,
                ExpectedBlockHeight = expectedBlockHeight,
                DiceCount = input.DiceCount == 0 ? 1 : input.DiceCount
            };
            State.BoutInformation[Context.OriginTransactionId] = boutInformation;
            Context.Fire(new Played()
            {
                PlayBlockHeight = boutInformation.PlayBlockHeight,
                PlayId = boutInformation.PlayId,
                PlayerAddress = boutInformation.PlayerAddress
            });
            return new PlayOutput { ExpectedBlockHeight = expectedBlockHeight };
        }

        public override Empty Bingo(Hash input)
        {
            Context.LogDebug(() => $"Getting game result of play id: {input.ToHex()}");

            CheckBingo(input, out var playerInformation, out var boutInformation, out var targetHeight);
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
                PlayerAddress = boutInformation.PlayerAddress
            });
            return new Empty();
        }

        public override Empty BingoNew(Empty input)
        {
            InitPlayerInfo(false);
            var expectedBlockHeight = Context.CurrentHeight.Add(BeangoTownContractConstants.BingoBlockHeight);
            var boutInformation = new BoutInformation
            {
                PlayBlockHeight = Context.CurrentHeight,
                PlayId = Context.OriginTransactionId,
                PlayTime = Context.CurrentBlockTime,
                PlayerAddress = Context.Sender,
                ExpectedBlockHeight = expectedBlockHeight,
                DiceCount = 1
            };
            State.BoutInformation[Context.OriginTransactionId] = boutInformation;
            var randomHash = State.ConsensusContract.GetRandomHash.Call(new Int64Value
            {
                Value = Context.CurrentHeight
            });

            Assert(randomHash != null && !randomHash.Value.IsNullOrEmpty(),
                "Still preparing your game result, please wait for a while :)");
            var playerInformation = State.PlayerInformation[Context.Sender];
            SetBoutInformationBingoInfo(boutInformation.PlayId, randomHash, playerInformation, boutInformation);
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
                PlayerAddress = boutInformation.PlayerAddress
            });
            return new Empty();
        }

        private List<int> GetDices(Hash hashValue, int diceCount)
        {
            var hexString = hashValue.ToHex();
            var dices = new List<int>();

            for (int i = 0; i < diceCount; i++)
            {
                var startIndex = i * 8;
                var intValue = int.Parse(hexString.Substring(startIndex, 8),
                    System.Globalization.NumberStyles.HexNumber);
                var dice = (intValue % 6 + 5) % 6 + 1;
                dices.Add(dice);
            }

            return dices;
        }

        private void SetPlayerInformation(PlayerInformation playerInformation, BoutInformation boutInformation)
        {
            playerInformation.CurGridNum = boutInformation.EndGridNum;
            playerInformation.SumScore = playerInformation.SumScore.Add(boutInformation.Score);
            State.PlayerInformation[boutInformation.PlayerAddress] = playerInformation;
        }

        private int GetPlayerCurGridNum(int preGridNum, int gridNum)
        {
            return (preGridNum + gridNum) %
                   State.GridTypeList.Value.Value.Count;
        }

        private void SetBoutInformationBingoInfo(Hash playId, Hash randomHash, PlayerInformation playerInformation,
            BoutInformation boutInformation)
        {
            var usefulHash = HashHelper.ConcatAndCompute(randomHash, playId);
            var dices = GetDices(usefulHash, boutInformation.DiceCount);
            var randomNum = dices.Sum();
            var curGridNum = GetPlayerCurGridNum(playerInformation.CurGridNum, randomNum);
            var gridType = State.GridTypeList.Value.Value[curGridNum];
            boutInformation.Score = GetScoreByGridType(gridType, usefulHash);
            boutInformation.DiceNumbers.AddRange(dices);
            boutInformation.GridNum = randomNum;
            boutInformation.StartGridNum = playerInformation.CurGridNum;
            boutInformation.EndGridNum = curGridNum;
            boutInformation.IsComplete = true;
            boutInformation.GridType = gridType;
            boutInformation.BingoBlockHeight = Context.CurrentHeight;
            State.BoutInformation[playId] = boutInformation;
        }

        private Int32 GetScoreByGridType(GridType gridType, Hash usefulHash)
        {
            int score;
            if (gridType == GridType.Blue)
            {
                score = BeangoTownContractConstants.BlueGridScore;
            }
            else if (gridType == GridType.Red)
            {
                score = BeangoTownContractConstants.RedGridScore;
            }
            else
            {
                score = Convert.ToInt32(Math.Abs(usefulHash.ToInt64() % 21) + 30);
            }

            return score;
        }

        private void CheckBingo(Hash playId, out PlayerInformation playerInformation,
            out BoutInformation boutInformation, out long targetHeight)
        {
            Assert(playId != null && !playId.Value.IsNullOrEmpty(), "Invalid playId.");

            playerInformation = State.PlayerInformation[Context.Sender];

            Assert(playerInformation != null, $"User {Context.Sender} not Login before.");

            boutInformation = State.BoutInformation[playId];

            Assert(boutInformation != null, "Bout not found.");

            Assert(!boutInformation!.IsComplete, "Bout already finished.");

            targetHeight = boutInformation.PlayBlockHeight.Add(BeangoTownContractConstants.BingoBlockHeight);
            Assert(targetHeight <= Context.CurrentHeight, "Invalid target height.");
        }

        public override BoolValue CheckBeanPass(Address owner)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BeangoTownContractConstants.BeanPassSymbol,
                Owner = owner
            });
            return new BoolValue { Value = getBalanceOutput.Balance > 0 };
        }

        public override Empty ChangeAdmin(Address newAdmin)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");

            if (State.Admin.Value == newAdmin)
            {
                return new Empty();
            }

            State.Admin.Value = newAdmin;
            return new Empty();
        }

        public override Address GetAdmin(Empty input)
        {
            return State.Admin.Value;
        }

        public override BoutInformation GetBoutInformation(GetBoutInformationInput input)
        {
            Assert(input!.PlayId != null && !input.PlayId.Value.IsNullOrEmpty(), "Invalid playId.");

            var boutInformation = State.BoutInformation[input.PlayId];

            Assert(boutInformation != null, "Bout not found.");

            return boutInformation;
        }

        public override PlayerInformation GetPlayerInformation(Address owner)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BeangoTownContractConstants.BeanPassSymbol,
                Owner = owner,
            });
            var nftEnough = getBalanceOutput.Balance > 0;

            var playerInformation = GetCurrentPlayerInformation(owner, nftEnough);
            return playerInformation;
        }

        private PlayerInformation GetCurrentPlayerInformation(Address playerAddress, bool nftEnough)
        {
            var playerInformation = State.PlayerInformation[playerAddress];
            if (playerInformation == null)
            {
                playerInformation = new PlayerInformation
                {
                    PlayerAddress = playerAddress,
                    SumScore = 0,
                    CurGridNum = 0
                };
            }

            var gameLimitSettings = State.GameLimitSettings.Value;
            playerInformation.PlayableCount = GetPlayableCount(gameLimitSettings, playerInformation, nftEnough);
            return playerInformation;
        }

        private Int32 GetPlayableCount(GameLimitSettings gameLimitSettings, PlayerInformation playerInformation,
            bool nftEnough)
        {
            if (!nftEnough) return 0;
            var now = Context.CurrentBlockTime.ToDateTime();
            var playCountResetDateTime =
                new DateTime(now.Year, now.Month, now.Day, gameLimitSettings.DailyPlayCountResetHours, 0, 0,
                    DateTimeKind.Utc).ToTimestamp();
            // LastPlayTime ,now must not be same DayField
            if (playerInformation.LastPlayTime == null || Context.CurrentBlockTime.CompareTo(
                                                           playerInformation.LastPlayTime.AddDays(1)
                                                       ) > -1
                                                       || (playerInformation.LastPlayTime.CompareTo(
                                                               playCountResetDateTime) == -1 &&
                                                           Context.CurrentBlockTime.CompareTo(playCountResetDateTime) >
                                                           -1))
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
            Assert(input.DailyPlayCountResetHours >= 0 && input.DailyPlayCountResetHours < 24,
                "Invalid dailyPlayCountResetHours.");
            Assert(input.DailyMaxPlayCount >= 0, "Invalid dailyMaxPlayCount.");
            State.GameLimitSettings.Value = input;
            return new Empty();
        }
    }
}