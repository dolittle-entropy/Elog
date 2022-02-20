# ELog - Displays event logs from Dolittle projects
Elog lets you connect your Dolittle eventsourced systems to the Dolittle runtime so that 
you can discover aggregates, find unique aggregate Ids (such as listing customers) as well as 
to see all historical events applied to those aggregates over time. 

## Using it 
If you just want to run elog without the fuss of building it, simply use your 
dotnet tool to install: 

```bash
$ dotnet tool install -g dolittle.elog
```



## Building it
ELog was developed using DotNet 6. You should download and install this
prior to building it. Once done, navigate to where you have the base
solution folder `src` and type: 

```bash
$ dotnet build -c Release 
```
You will then find the executable in the folder:

`./Elog/bin/Release/net6.0`

For simplicity, the recommendation is that you add this entire folder to 
your `$PATH` variable. 

## Initial configuration
In order to run, you need to configure a project for Elog. The first configuration that you create  will become the default project. 

```bash
$ elog config
```
Run the `elog config` command to get started. 

> For the sake of running different projects, or to see event streams in multiple microservices, configure each microservice with an easy-to-remember name. 


## Usage

Once you have at least one configuration, you can invoke elog by typing:

```bash
$ elog run
```

This will list all aggregates found in that configuration
Next, to see a list of all unique id's for a specific aggregate, simply provide that aggregate name:

```bash
$ elog -a Customer
```
In the example above, we have selected the `Customer` aggregate, which will give us a list of unique customers currently in the event log as well as the event count for each

```bash
$ elog -a Customer -id 78dc3f83-a45d-4cd2-acf6-9eb9c6dcac60
```
By applying the -id of the specific customer, we will now see a list of what events that the 
aggregate has applied to that particular customer including the date. 

The events are numbered. If you wish to see the actual payload of a particular event, you can add the event index number with the -n parameter: 

```bash
$ elog -a Customer -id 78dc3f83-a45d-4cd2-acf6-9eb9c6dcac60 -n 3
```
The example above displays the payload of event number 3 from the previous list

## Selecting configuration
Elog will work from the configuration that is marked as `active`. 
If you have several configurations and want to change the active configuration, 
just type `elog config -a` to bring up the active configuration selector.

```bash
$ elog configure -a
```

----
End of README

