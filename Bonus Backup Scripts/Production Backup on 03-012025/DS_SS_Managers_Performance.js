/**
 * @NApiVersion 2.x
 * @NScriptType ScheduledScript
 */
define(['N/format', 'N/log', 'N/record', 'N/search'], function (format, log, record, search) {
    function execute(context) {
        try {
            var currentDate = new Date();
            var currentMonth = currentDate.getMonth(); // June = 5, Dec = 11
            var currentYear = currentDate.getFullYear();
            if (currentMonth !== 4 && currentMonth !== 11) {
                log.audit("Script Skipped", "This script only runs June & Dec.");
                return;
            }

            var year = {
                "2020": 1, "2021": 2, "2022": 3, "2023": 4, "2024": 5, "2025": 6, "2026": 7, "2027": 8, "2028": 9, "2029": 10, "2030": 11, "2031": 12,
                "2032": 13, "2033": 14, "2034": 15, "2035": 16, "2036": 17, "2037": 18, "2038": 19, "2039": 20, "2040": 21
            };

            var managersBonusCalSearch = search.create({
                type: "customrecord_bonus_calculation",
                filters:
                    [
                        ["custrecord_employee_bonus.custentity_manager", "noneof", "@NONE@"],
                        "AND",
                        ["isinactive", "is", "F"]
                    ],
                columns:
                    [
                        search.createColumn({
                            name: "custentity_manager",
                            join: "CUSTRECORD_EMPLOYEE_BONUS",
                            summary: "GROUP",
                            label: "Manager"
                        }),
                        search.createColumn({
                            name: "custrecord_employee_bonus",
                            summary: "COUNT",
                            label: "Employee"
                        })
                    ]
            });
            var managersearchResultCount = managersBonusCalSearch.runPaged().count;
            log.debug("managersBonusCalSearch result count", managersearchResultCount);
            managersBonusCalSearch.run().each(function (empResults) {
                var csmEmployee = empResults.getValue({
                    name: "custentity_manager",
                    join: "CUSTRECORD_EMPLOYEE_BONUS",
                    summary: "GROUP"
                });
                var employeeCount = empResults.getValue({
                    name: "custrecord_employee_bonus",
                    summary: "COUNT"
                });
                // CSM Search
                var weightedAveragesArray = [];
                var weightedAverages = 0;
                var h1H2 = currentMonth == 4 ? 1 : 2;
                var csmBonusSearch = search.create({
                    type: "customrecord_bonus_calculation",
                    filters:
                        [
                            ["custrecord_employee_bonus.custentity_manager", "anyof", csmEmployee],
                            "AND",
                            ["isinactive", "is", "F"],
                            "AND",
                            ["custrecord_h1_h2_bonus", "anyof", h1H2],
                            "AND",
                            ["custrecord_year_bonus", "anyof", year[currentYear]],
                        ],
                    columns:
                        [
                            search.createColumn({ name: "custrecord_employee_bonus", label: "Employee" }),
                            search.createColumn({ name: "custrecord_weighted_average_percent", label: "Weighted Average %" })
                        ]
                });
                var csmCount = csmBonusSearch.runPaged().count;
                log.debug("csmBonusSearch result count", csmCount);
                csmBonusSearch.run().each(function (csmResult) {
                    var weightedAverage = parseFloat(csmResult.getValue('custrecord_weighted_average_percent')); // Example: 85%
                    weightedAveragesArray.push(weightedAverage);
                    log.debug("Weighted Averages Array", weightedAveragesArray);
                    var average = parseFloat(100 / csmCount); // Example: 100/3 = 33.33%
                    average = parseFloat(average / 100); // Example: 33.33/100 = 0.3333
                    weightedAverage = parseFloat(weightedAverage / 100); // Example: 85/100 = 0.85
                    var finalAverage = parseFloat(average * weightedAverage); // Example: 0.3333 * 0.85 = 0.2833
                    weightedAverages += finalAverage;
                    log.debug("weightedAverages", weightedAverages);
                    return true;
                });
                log.debug("Final Weighted Average", weightedAverages);
                if (weightedAverages) {
                    var customrecord_manager_performance_evalSearchObj = search.create({
                        type: "customrecord_manager_performance_eval",
                        filters:
                            [
                                ["custrecord_manager", "anyof", csmEmployee],
                                "AND",
                                ["custrecord_year_performance", "anyof", year[currentYear]],
                                "AND",
                                ["custrecord_h1_h2_performance", "anyof", h1H2]
                            ],
                        columns:
                            [
                                search.createColumn({ name: "internalid", label: "Internal ID" }),
                                search.createColumn({ name: "custrecord_manager", label: "Manager" }),
                                search.createColumn({ name: "custrecord_year_performance", label: "Year" }),
                                search.createColumn({ name: "custrecord_h1_h2_performance", label: "H1/ H2" })
                            ]
                    });
                    var managersCount = customrecord_manager_performance_evalSearchObj.runPaged().count;
                    log.debug("customrecord_manager_performance_evalSearchObj result count", managersCount);
                    customrecord_manager_performance_evalSearchObj.run().each(function (managersResults) {
                        var managerInternalId = managersResults.getValue({
                            name: "internalid",
                            label: "Internal ID"
                        });
                        var managerRecord = record.load({
                            type: "customrecord_manager_performance_eval",
                            id: managerInternalId,
                            isDynamic: true
                        });
                        managerRecord.setValue({
                            fieldId: "custrecord_manager_weighted_average",
                            value: weightedAverages * 100
                        });

                        for (var i = 0; i < weightedAveragesArray.length; i++) {
                            var fieldId = 'custrecord_csm_' + (i + 1) + '_percent';
                            managerRecord.setValue({
                                fieldId: fieldId,
                                value: weightedAveragesArray[i]
                            });
                        }

                        var managerRecId = managerRecord.save();
                        log.debug("Manager Record Updated", "Manager ID: " + managerRecId + ", Contribution: " + weightedAverages);
                        return true;
                    });
                }
                return true;
            });


        } catch (error) {
            log.error("Error in Creating Revenue Upsell Records", error);
        }
    }

    return {
        execute: execute
    };
});