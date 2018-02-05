#!/bin/bash

FILE="./travis/index.html"

function preList {
    echo "<!DOCTYPE html><html><body>
<h3 class="list-heading">a list of the lastest few builds:</h3>
<ul>"
}

preList > $FILE

function postList {
    echo "</ul></body></html>"
}

function generateLink {
    IFS=' ' read -a myarray <<< $1
    echo "<li><a href=\""${myarray[0]}"\">"${myarray[1]}"</a></li>" >> $FILE
}


readarray array < ./travis/builds_index.txt

for (( idx=${#array[@]}-1 ; idx>=0 ; idx-- )) ; do
    generateLink "${array[idx]}"
done

postList >> $FILE
