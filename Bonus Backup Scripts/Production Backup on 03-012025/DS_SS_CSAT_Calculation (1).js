/**
 * @NApiVersion 2.x
 * @NScriptType ScheduledScript
 */
define(['N/format', 'N/log', 'N/record', 'N/search'], function (format, log, record, search) {
    function execute(context) {
        try {
            var year = {
                "2020": 1, "2021": 2, "2022": 3, "2023": 4, "2024": 5, "2025": 6, "2026": 7, "2027": 8, "2028": 9, "2029": 10, "2030": 11,
                "2031": 12, "2032": 13, "2033": 14, "2034": 15, "2035": 16, "2036": 17, "2037": 18, "2038": 19, "2039": 20, "2040": 21
            };
            var currentDate = new Date();
            var currentYear = currentDate.getFullYear(); // example: 2025
            var currentMonth = currentDate.getMonth() + 1; // example: 6 (June)
            var h1H2 = currentMonth <= 6 ? 1 : 2;
            // if (currentMonth <= 6 ) {
            //     var h1H2 = 1
            // }
            // else if (currentMonth <= 12) {
            //     var h1H2 = 2
            // }
            var employeeSearchObj = search.create({
                type: "employee",
                filters:[
                    ["salesrole", "anyof", "1"],
                    "AND",
                    ["isinactive", "is", "F"]
                ],
                columns:[
                    search.createColumn({ name: "entityid", label: "Name" }),
                    search.createColumn({ name: "internalid", label: "Internal ID" })
                ]
            });
            var csmSearchResultCount = employeeSearchObj.runPaged().count;
            log.debug("employeeSearchObj result count", csmSearchResultCount);
            employeeSearchObj.run().each(function (csmResults) {
                var csmEmployee = csmResults.getValue({ name: "internalid" });

                var numberofCusAverage = 0;
                var customrecord_csat_scoreSearchObj = search.create({
                    type: "customrecord_csat_score",
                    filters:[
                        ["isinactive", "is", "F"],
                        "AND",
                        ["custrecord_year_csat", "anyof", year[currentYear]],
                        "AND",
                        ["custrecord_employee_csat", "anyof", csmEmployee],
                        "AND",
                        ["custrecord_h1_h2_csat", "anyof", h1H2],
                        "AND",
                        ["created", "on", "today"]
                    ],
                    columns:[
                        search.createColumn({ name: "custrecord_employee_csat", summary: "GROUP", label: "Employee" }),
                        search.createColumn({ name: "custrecord_no_of_customers", summary: "GROUP", label: "No. Of Customers" })
                    ]
                });
                var csatSearchResultCount = parseInt(customrecord_csat_scoreSearchObj.runPaged().count);
                log.debug("customrecord_csat_scoreSearchObj result count", csatSearchResultCount);
                if (csatSearchResultCount) {
                    customrecord_csat_scoreSearchObj.run().each(function (csatResults) {

                        var numberOfCustomers = csatResults.getValue({
                            name: "custrecord_no_of_customers",
                            summary: "GROUP"
                        });
                        numberOfCustomers = parseInt(numberOfCustomers);
                        log.debug("numberOfCustomers", numberOfCustomers);
                        numberofCusAverage += numberOfCustomers;
                        return true;
                    });
                    var finalAverage = numberofCusAverage / csatSearchResultCount;
                    log.debug("finalAverage", finalAverage);

                    // dec or june rec search
                    var juneDecMonth = currentMonth <= 6 ? 6 : 12;
                    var june_dec_record_search = search.create({
                        type: "customrecord_csat_score",
                        filters:[
                            ["isinactive", "is", "F"],
                            "AND",
                            ["custrecord_year_csat", "anyof", year[currentYear]],
                            "AND",
                            ["custrecord_employee_csat", "anyof", csmEmployee],
                            "AND",
                            ["custrecord_h1_h2_csat", "anyof", h1H2],
                            "AND",
                            ["custrecord_month_csat", "anyof", juneDecMonth],
                            "AND",
                            ["created", "on", "today"]
                        ],
                        columns:[
                            search.createColumn({ name: "custrecord_employee_csat", label: "Employee" }),
                            search.createColumn({ name: "internalid", label: "Internal ID" })
                        ]
                    });
                    var searchResultCount = june_dec_record_search.runPaged().count;
                    log.debug("june_dec_record_search result count", searchResultCount);
                    june_dec_record_search.run().each(function (juneDecResults) {
                        var juneDecRecId = juneDecResults.getValue('internalid');
                        var juneDecRec = record.load({
                            type: "customrecord_csat_score",
                            id: juneDecRecId,
                            isDynamic: true
                        });
                        juneDecRec.setValue({
                            fieldId: "custrecord_static_weighted_avg",
                            value: finalAverage
                        });
                        var juneDecId = juneDecRec.save();
                        log.debug("juneDecId", juneDecId);
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