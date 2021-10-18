# ELog - An event logging tool for the Dolittle runtimes / SDK
Elog lets you connect your Dolittle eventsourced systems to the Dolittle runtime so that 
you can discover aggregates, find unique aggregate Ids (such as listing customers) as well as 
to see all events produced by those aggregates in sequence. 

## Initial configuration
In order to run, you need to configure a project for Elog. The first configuration that you
add will become the default project. 

```bash
$ elog configure
```
Run the Elog configure command to start the configuration wizard. 
For the sake of comparing different projects, or to see event streams in multiple microservices, 
configure each microservice with an easy-to-remember name. You can list your configurations by
invoking the following command:

```bash
$ elog configure --list
```

You can also delete configurations by providing the name of the configuration you no longer need:

```bash
$ elog configure -delete Default
```

## Usage

Once you have at least one configuration, you can invoke elog by just typing its name:

```bash
$ elog
```

This will list all aggregates found in that configuration
Next, to see a list of all unique id's for a specific aggregate, simply provide that aggregate name:

```bash
$ elog -a Customer
```
In the example above, we have selected the `Customer` aggregate, which will give us a list of 
unique customers currently in the event log as well as the event count for each

```bash
$ elog -a Customer -id 78dc3f83-a45d-4cd2-acf6-9eb9c6dcac60
```
By applying the -id of the specific customer, we will now see a list of what events that the 
aggregate has applied to that particular customer. You will see a list of events that have 
been applied on that Aggregate, including the date. These events are ordered and numbered. If you
wish to see the details of a particular event, you can simply add the index number with the -evt 
parameter: 

```bash
$ elog -a Customer -id 78dc3f83-a45d-4cd2-acf6-9eb9c6dcac60 -evt 3
```
The example above displays the payload of event number 3 from the previous list

## Selecting configuration

If you want to target a different project using Elog, simply add the --configuration parameter to the command: 

```bash
$ elog -c MyConfig -a Customer -id 78dc3f83-a45d-4cd2-acf6-9eb9c6dcac60
```
The `-c MyConfig` is telling Elog to load the configuration named `MyConfig` 
Note that if the configuration doesen't exist, you will get an error. 

----
End of README

