# beangotown-contract
BRANCH | AZURE PIPELINES                                                                                                                                                                                                                   | TESTS                                                                                                                                                                                                  | CODE COVERAGE
-------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------
MASTER   | [![Build Status](https://dev.azure.com/BeangoTown/beangotown-contract/_apis/build/status/beangotown-contract?branchName=master)](https://dev.azure.com/BeangoTown/beangotown-contract/_build/latest?definitionId=2&branchName=master) | [![Test Status](https://img.shields.io/azure-devops/tests/BeangoTown/beangotown-contract/2/master)](https://dev.azure.com/BeangoTown/beangotown-indexer/_build/latest?definitionId=2&branchName=master) | [![codecov](https://codecov.io/github/Beangotown/beangotown-contract/graph/badge.svg?token=23L68PUVX8)](https://codecov.io/github/Beangotown/beangotown-contract)
DEV    | [![Build Status](https://dev.azure.com/BeangoTown/beangotown-contract/_apis/build/status/beangotown-contract?branchName=dev)](https://dev.azure.com/BeangoTown/beangotown-contract/_build/latest?definitionId=2&branchName=dev)   | [![Test Status](https://img.shields.io/azure-devops/tests/BeangoTown/beangotown-contract/2/dev)](https://dev.azure.com/BeangoTown/beangotown-indexer/_build/latest?definitionId=2&branchName=dev)       | [![codecov](https://codecov.io/github/Beangotown/beangotown-contract/graph/badge.svg?token=23L68PUVX8)](https://codecov.io/github/Beangotown/beangotown-contract)
## **Introduction**

BeanGo Town is a game built on the AELF blockchain. Inspired by Monopoly. Users need to obtain the NFT to start the game. Click the GO button to roll the dice to determine the number of steps to move, and receive corresponding point rewards based on the location the piece lands on after the move.

- The game supports multiple ways to log in or register (Web2/Web3).
- The BeanPass NFT required to play the game can be collected directly in the game.
- The number of points rolled in the game is determined by random numbers generated on the chain.
## **How to use**

Before cloning the code and deploying the  Contracts BeangoTown, command dependencies, and development tools are needed. You can follow:

- [Common dependencies](https://aelf-boilerplate-docs.readthedocs.io/en/latest/overview/dependencies.html)
- [Building sources and development tools](https://aelf-boilerplate-docs.readthedocs.io/en/latest/overview/tools.html)

The following command will clone Contracts BeangoTown into a folder. Please open a terminal and enter the following command:

```Bash
git clone https://github.com/Beangotown/beangotown-contract
```

The next step is to build the contract to ensure everything is working correctly. Once everything is built, you can run as follows:

```Bash
# enter the Launcher folder and build 
cd src/AElf.Boilerplate.BeangoTownContract.Launcher

# build
dotnet build

# run the node 
dotnet run
```

It will run a local temporary aelf node and automatically deploy the  Contracts BeangoTown. You can access the node from `localhost:1235`.

This temporary aelf node runs on a framework called Boilerplate for deploying smart contracts easily. When running it, you might see errors showing incorrect password. To solve this, you need to back up your `aelf/keys`folder and start with an empty keys folder. Once you have cleaned the keys folder, stop and restart the node with `dotnet run`command shown above. It will automatically generate a new aelf account for you. This account will be used for running the aelf node and deploying the  Contracts BeangoTown.

## **Test**

You can easily run unit tests on  Contracts BeangoTown. Navigate to the Contracts.BeangoTownContract.Tests and run:

```Bash
cd ../../test/Contracts.BeangoTownContract.Tests
dotnet test

## Contributing

We welcome contributions to the BeangoTown Contract project. If you would like to contribute, please fork the repository and submit a pull request with your changes. Before submitting a pull request, please ensure that your code is well-tested and adheres to the aelf coding standards.

## License

BeangoTown Contract is licensed under [MIT](https://github.com/Beangotown/beangotown-contract/blob/master/LICENSE).
