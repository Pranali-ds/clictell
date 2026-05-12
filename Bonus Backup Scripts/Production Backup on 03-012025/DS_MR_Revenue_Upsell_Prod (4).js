/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 */
define(['N/search', 'N/log', 'N/format', 'N/record'], function (search, log, format, record) {

    function getInputData() {
        var months = {
            1: "JAN", 2: "FEB", 3: "MAR", 4: "APR", 5: "MAY", 6: "JUN",
            7: "JUL", 8: "AUG", 9: "SEP", 10: "OCT", 11: "NOV", 12: "DEC"
        };

        var specificMonths = [1, 6, 7, 12]; // January, June, July, December
   
        var revUpsellData = {};
        var startDate = new Date(2024, 0, 1); // January 2024
        var endDate = new Date(); // Current date
   
        while (startDate <= endDate) {
            var currentMonth = startDate.getMonth() + 1; // Months are 0-indexed in JavaScript
   
            // Check if the current month is one of the specific months
            if (specificMonths.includes(currentMonth)) {
                var nextMonth = new Date(startDate.getFullYear(), startDate.getMonth() + 1, 1);
                var beforeMonth = new Date(startDate.getFullYear(), startDate.getMonth() - 2, 1);
                log.debug("Months", "Current Month" + startDate + ", Before Month: " + beforeMonth);
                var formattedStartDate = format.format({
                    value: startDate,
                    type: format.Type.DATE
                });
                var formattedEndDate = format.format({
                    value: new Date(nextMonth - 1),
                    type: format.Type.DATE
                });
   
                log.debug("Processing Period", {
                    startDate: formattedStartDate,
                    endDate: formattedEndDate
                });
               //New customer: first sale date within the month
                var customrecord_commissionSearchObj = search.create({
                    type: "customrecord_commission",
                    filters: [
                        ["custrecord_commission_inv.mainline", "is", "T"],
                        "AND",
                        ["custrecord_commission_emp.salesrole", "anyof", "1"],
                        "AND",
                        ["custrecord_commission_inv.trandate", "within", formattedStartDate, formattedEndDate],
                        "AND",
                        ["custrecord_commission_customer.custentity_cls_in_customer", "anyof", "12", "22", "5", "27", "25"]
                    ],
                    columns: [
                        search.createColumn({
                            name: "custrecord_commission_emp",
                            summary: "GROUP"
                        }),
                        search.createColumn({
                            name: "amount",
                            join: "CUSTRECORD_COMMISSION_INV",
                            summary: "SUM"
                        }),
                        search.createColumn({
                            name: "firstsaledate",
                            join: "CUSTRECORD_COMMISSION_CUSTOMER",
                            summary: "GROUP"
                        })
                    ]
                });
                customrecord_commissionSearchObj.run().each(function(result) {
                    var revMonth = startDate.getMonth() + 1;
                    var revYear = startDate.getFullYear();
                    var revMonthYear = months[revMonth] + "-" + revYear;
   
                    var csmEmployee = result.getValue({
                        name: "custrecord_commission_emp",
                        summary: "GROUP"
                    });
                    var firstSaleDate = result.getValue({
                        name: "firstsaledate",
                        join: "CUSTRECORD_COMMISSION_CUSTOMER",
                        summary: "GROUP"
                    });
                    var invAmount = parseFloat(result.getValue({
                        name: "amount",
                        join: "CUSTRECORD_COMMISSION_INV",
                        summary: "SUM"
                    })) || 0;
   
                    var key = `${csmEmployee} - ${revMonthYear}`;
                    if (!revUpsellData[key]) {
                        revUpsellData[key] = {
                            csmEmployee: csmEmployee,
                            revMonthYear: revMonthYear,
                            totalRevenue: 0,
                            newCustomerRevenue: 0,
                            // twoMonthsPrior: 0,
                            monthEndRevenue: 0
                        };
                    }
                    revUpsellData[key].totalRevenue += invAmount;
                    log.debug("New Customer Dates", "First Sale Date: " + firstSaleDate + ", Start Date: " + startDate + ", Next Month: " + nextMonth + ", Before Month: " + beforeMonth);
                    if (firstSaleDate && (new Date(firstSaleDate) >= startDate) && (new Date(firstSaleDate) <= nextMonth)) {
                        revUpsellData[key].newCustomerRevenue += invAmount;
                    }
                    // if (firstSaleDate && (new Date(firstSaleDate) < beforeMonth)) {
                        // }
                    // if ((startDate > beforeMonth) && (new Date(firstSaleDate) < beforeMonth)) {
                    //     revUpsellData[key].twoMonthsPrior += invAmount;
                    // }
                    if ((startDate > beforeMonth) && (new Date(firstSaleDate) < beforeMonth)) {
                        revUpsellData[key].monthEndRevenue += invAmount;
                    }
                    return true;
                });
            }
   
            startDate = new Date(startDate.getFullYear(), startDate.getMonth() + 1, 1);
        }
        revUpsellData.count = Object.values(revUpsellData).length;
        log.debug("Final Revenue Data", revUpsellData);
        log.debug("Final Revenue Data Length", Object.values(revUpsellData).length);
        count = Object.values(revUpsellData).length;
        return Object.values(revUpsellData);
    }

    function map(context) {
        try {
            var customerMetrics = JSON.parse(context.value);
            // customerMetrics.totalRecords = count;
            log.debug("Processing Context", customerMetrics);
            context.write({
                key: customerMetrics.csmEmployee + " - " + customerMetrics.revMonthYear,
                value: customerMetrics
            });
        } catch (e) {
            log.error("Error in map function", e.toString());
        }
    }

    function reduce(context) {
        try {
            var data = JSON.parse(context.values[0]);
            var monthMap = {
                "JAN": 1, "FEB": 2, "MAR": 3, "APR": 4, "MAY": 5, "JUN": 6,
                "JUL": 7, "AUG": 8, "SEP": 9, "OCT": 10, "NOV": 11, "DEC": 12
            };
           
            var yearMap = {
                "2020": 1, "2021": 2, "2022": 3, "2023": 4, "2024": 5, "2025": 6, "2026": 7, "2027": 8, "2028": 9, "2029": 10, "2030": 11,
                "2031": 12, "2032": 13, "2033": 14, "2034": 15, "2035": 16, "2036": 17, "2037": 18, "2038": 19, "2039": 20, "2040": 21
            };
               
            if (typeof data === "object") {
                log.debug("Processing Data", data);
               
                var [splitMonth, splitYear] = data.revMonthYear.split("-");
                log.debug("Parsed Month-Year", { Month: splitMonth, Year: splitYear });
               
                var customrecord_revenue_upsellSearchObj = search.create({
                    type: "customrecord_revenue_upsell",
                    filters: [
                        ["custrecord_employee_rev", "anyof", data.csmEmployee],
                        "AND",
                        ["custrecord_month_rev", "anyof", monthMap[splitMonth]],
                        "AND",
                        ["custrecord_year_rev", "anyof", yearMap[splitYear]]
                    ],
                    columns: [
                        search.createColumn({ name: "internalid" })
                    ]
                });
               
                var searchResultCount = customrecord_revenue_upsellSearchObj.runPaged().count;
                log.debug("Record Count", searchResultCount);
               
                var revUpsellRec;
                // var monthEndRevenue = parseFloat(data.totalRevenue) - parseFloat(data.newCustomerRevenue) - parseFloat(data.twoMonthsPrior);
                // log.debug("Month End Revenue", monthEndRevenue);
                var twoMonthsPrior = parseFloat(data.totalRevenue) - parseFloat(data.newCustomerRevenue) - parseFloat(data.monthEndRevenue);
                log.debug("Two Months Prior", twoMonthsPrior);
                if (searchResultCount == 0) {
                    revUpsellRec = record.create({
                        type: "customrecord_revenue_upsell",
                        isDynamic: true
                    });
                    revUpsellRec.setValue("custrecord_employee_rev", data.csmEmployee);
                    revUpsellRec.setValue("custrecord_month_rev", monthMap[splitMonth]);
                    revUpsellRec.setValue("custrecord_year_rev", yearMap[splitYear]);
                } else {
                    var searchResult = customrecord_revenue_upsellSearchObj.run().getRange({ start: 0, end: 1 });
                    if (searchResult.length > 0) {
                        revUpsellRec = record.load({
                            type: "customrecord_revenue_upsell",
                            id: searchResult[0].getValue("internalid")
                        });
                    }
                }
               
                if (revUpsellRec) {
                    revUpsellRec.setValue("custrecord_total_revenue", data.totalRevenue);
                    revUpsellRec.setValue("custrecord_new_customers_revnue", data.newCustomerRevenue);
                    revUpsellRec.setValue("custrecord_customers_prior_2_months_reve", twoMonthsPrior);
                    revUpsellRec.setValue("custrecord_month_end_revenue", data.monthEndRevenue);
                    revUpsellRec.setValue("custrecord_h1_h2_rev", monthMap[splitMonth] <= 6 ? 1 : 2);
                   
                    var savedRecId = revUpsellRec.save();
                    log.debug("Saved Record ID", savedRecId);
                }
            }
            else{
                var currentYear = new Date().getFullYear();
                log.debug("Current Year", currentYear);
                var employeeSearchResults = search.create({
                    type: "employee",
                    filters: [
                        ["salesrole", "anyof", "1"],
                        "AND",
                        ["isinactive", "is", "F"]
                    ],
                    columns: ["internalid"]
                }).run().getRange({ start: 0, end: 1000 });

                employeeSearchResults.forEach(function (employee) {
                    var csmEmployeeId = employee.getValue('internalid'); // Process only the current year
                    for(var key in yearMap){
                        if (parseInt(key) !== currentYear) continue;
                        log.debug("Current Year", currentYear);

                        // January and June
                        var janRevenue = 0;
                        var junRevenue = 0;
                        var j_j_revenue_upsellSearchObj = search.create({
                            type: "customrecord_revenue_upsell",
                            filters:
                            [
                                ["isinactive","is","F"],
                                "AND",
                                ["custrecord_year_rev","anyof", yearMap[key]],
                                "AND",
                                ["custrecord_month_rev","anyof","1","6"],
                                "AND",
                                ["custrecord_employee_rev","anyof", csmEmployeeId]
                            ],
                            columns:
                            [
                                search.createColumn({name: "internalid", label: "Internal ID"}),
                                search.createColumn({name: "custrecord_employee_rev", label: "Employee"}),
                                search.createColumn({name: "custrecord_month_rev", label: "Month"}),
                                search.createColumn({name: "custrecord_month_end_revenue", label: "Month End Revenue"})
                            ]
                        });
                        var searchResultCount = j_j_revenue_upsellSearchObj.runPaged().count;
                        log.debug("j_j_revenue_upsellSearchObj result count",searchResultCount);
                        j_j_revenue_upsellSearchObj.run().each(function(result){
                            var kpiMonth = result.getValue('custrecord_month_rev');
                            log.debug("Kpi month H1*****",kpiMonth);

                            if (kpiMonth == 1){
                                janRevenue = result.getValue('custrecord_month_end_revenue');
                                log.debug("January Revenue", janRevenue);
                            }
                            if (kpiMonth == 6){
                                junRevenue = result.getValue('custrecord_month_end_revenue');
                                var juneRecId = result.getValue('internalid');
                                log.debug("June Revenue", junRevenue);
                                log.debug("June Record ID", juneRecId);
                            }
                            if (juneRecId) {
                                var kpiPercentage = ((junRevenue - janRevenue) / janRevenue) * 100;
                                log.debug("KPI Percentage", kpiPercentage);
                                var kpiAchievement = 0;
                                if (kpiPercentage >= 25) {
                                    kpiAchievement = 125;
                                } else if (kpiPercentage >= 15 && kpiPercentage < 25) {
                                    kpiAchievement = 100;
                                } else if (kpiPercentage >= 5 && kpiPercentage < 15) {
                                    kpiAchievement = 75;
                                } else if (kpiPercentage < 5) {
                                    kpiAchievement = 0;
                                }
                                var juneRecord = record.load({
                                   type: "customrecord_revenue_upsell",
                                   id: juneRecId
                                });
                                juneRecord.setValue("custrecord_kpi_achievement_rev", kpiAchievement + " %");
                                var savedJunRec = juneRecord.save();
                                log.debug("Saved June Record", savedJunRec);
                            }
                            return true;
                        });

                        // July - December
                        var j_d_revenue_upsellSearchObj = search.create({
                            type: "customrecord_revenue_upsell",
                            filters:
                            [
                                ["isinactive","is","F"],
                                "AND",
                                ["custrecord_year_rev","anyof", yearMap[key]],
                                "AND",
                                ["custrecord_month_rev","anyof","7","12"],
                                "AND",
                                ["custrecord_employee_rev","anyof", csmEmployeeId]
                            ],
                            columns:
                            [
                                search.createColumn({name: "internalid", label: "Internal ID"}),
                                search.createColumn({name: "custrecord_employee_rev", label: "Employee"}),
                                search.createColumn({name: "custrecord_month_rev", label: "Month"}),
                                search.createColumn({name: "custrecord_month_end_revenue", label: "Month End Revenue"})
                            ]
                        });
                        var searchResultCount = j_d_revenue_upsellSearchObj.runPaged().count;
                        log.debug("j_d_revenue_upsellSearchObj result count",searchResultCount);
                        j_d_revenue_upsellSearchObj.run().each(function(result){
                            var kpiMonth = result.getValue('custrecord_month_rev');
                            log.debug("kpiMonth",kpiMonth);
                            if (kpiMonth == 7){
                                var julRevenue = result.getValue('custrecord_month_end_revenue');
                            }
                            if(kpiMonth == 12){
                                var decRevenue = result.getValue('custrecord_month_end_revenue');
                                var decRecId = result.getValue('internalid');
                               
                            }
                            if (decRecId) {
                                var kpiPercentage = ((decRevenue - julRevenue) / julRevenue) * 100;
                                log.debug("KPI Percentage", kpiPercentage);
                                var kpiAchievement = 0;
                                if (kpiPercentage >= 25) {
                                    kpiAchievement = 125;
                                } else if (kpiPercentage >= 15 && kpiPercentage < 25) {
                                    kpiAchievement = 100;
                                } else if (kpiPercentage >= 5 && kpiPercentage < 15) {
                                    kpiAchievement = 75;
                                } else if (kpiPercentage < 5) {
                                    kpiAchievement = 0;
                                }
                                var decRecord = record.load({
                                   type: "customrecord_revenue_upsell",
                                   id: decRecId
                                });
                                decRecord.setValue("custrecord_kpi_achievement_rev", kpiAchievement + " %");
                                var savedDecRec = decRecord.save();
                                log.debug("Saved December Record", savedDecRec);
                            }
                            return true;
                        });
                    }
                });
            }
        } catch (e) {
            log.error("Error in reduce function", e.toString());
        }
    }

    function summarize(summary) {
        log.audit("MapReduce Execution Summary", {
            totalProcessed: summary.inputSummary.recordCount
        });
    }

    return {
        getInputData: getInputData,
        map: map,
        reduce: reduce,
        summarize: summarize
    };
});