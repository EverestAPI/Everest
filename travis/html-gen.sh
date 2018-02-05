#!/bin/bash

FILE="./travis/index.html"

function preList {
    echo "<html><body>
a list of the lastest few builds:
<ul>"
}

function postList {
    echo "</ul></body></html>"
}

function generateLink {
    IFS=' ' read -a myarray <<< $1
	if [[ ${myarray[0]} != /everest-travis* ]] ; then
	    return
	fi
	echo "<li><a href=\""${myarray[0]}"\">"${myarray[1]}"</a></li>" >> FILE
}

preList >> FILE
readarray array < builds_index.txt

for (( idx=${#array[@]}-1 ; idx>=0 ; idx-- )) ; do
    generateLink "${array[idx]}"
done

postList >> FILE